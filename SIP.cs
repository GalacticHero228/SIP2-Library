/**************************************************************
 * 
 *  (c) 2017 Mark Lesniak - Nice and Nerdy LLC
 *  
 * 
 *  Implementation of the Standard Interchange Protocol version 
 *  2.0.  Used to standardize queries across multiple database
 *  architectures.  
 *  
 *  Permission is hereby granted, free of charge, to any person obtaining a copy
 *  of this software and associated documentation files (the "Software"), to deal
 *  in the Software without restriction, including without limitation the rights
 *  to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
 *  copies of the Software, and to permit persons to whom the Software is
 *  furnished to do so, subject to the following conditions:
 *
 *  The above copyright notice and this permission notice shall be included in
 *  all copies or substantial portions of the Software.
 *
 *  THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
 *  IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
 *  FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
 *  AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
 *  LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
 *  OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
 *  THE SOFTWARE.
 *  
 * 
**************************************************************/
/**************************************************************
 * 
 *  (c) 2018 Chris Burton
 *  
 *  Made many changes to library on 06-26-2018
 *  -Changed Patrons.cs and Item.cs to use case/switch as opposed to if statements which they were using. This speeds up query time and cleans this up.
 *  -Added methods to parse customer data and item data.
 *  -Added method and indicators to allow returning items in a desired status.
 *  -Added method to compare Holds vs. Unavailable Holds so a list of available hold items can be returned
 *  -Added Patron Home method to return the possiblity of a reciprocal borrower
 *  -Modified library to work with the Evergreen ILS
 *  -Created Unit Tests to test the library for functionality
 *  -Added identifiers to AuthorizeBarcode for better indication of why a card has not been authenticated (Blocked, Fines, Expired)
 *  -Added more indicators to both patrons and items for more robust reporting
 * 
**************************************************************/

using System;
using System.Text;
using System.Net;
using System.Net.Sockets;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Globalization;

namespace SIP2
{
    public class SipConnection : IDisposable
    {
        /*********************************************
         * Instance Variables
         *********************************************/

        private IPAddress ipAddress;
        private IPEndPoint remoteEP;
        private Socket sender = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        private bool connected = false;
        private bool authorized = false;
        private int incrementer = 0;

        private string domain = String.Empty;
        private string port = String.Empty;
        private string username = String.Empty;
        private string password = String.Empty;
        private string extra_number = String.Empty;

        private delegate string SipFactory(string sipCommand);
        private SipFactory sipFactory;
        private bool hasChecksum;

        private string holdsIdentifier = "Y";
        private string overdueIdentifier = " Y";
        private string chargedIdentifier = "  Y";
        private string finedIdentifier = "   Y";
        private string recallIdentifier = "    Y";
        private string unaHoldsIdentifier = "     Y";
        

        /*********************************************
        * Public Properties
        *********************************************/

        public bool HasChecksum
        {
            get
            {
                return hasChecksum;
            }

            set
            {
                if (value == true)
                {
                    sipFactory = null;
                    sipFactory += SipFactoryWithCheckSum;
                    hasChecksum = value;
                }
                else if (value == false)
                {
                    sipFactory = null;
                    sipFactory += SipFactoryNoCheckSum;
                    hasChecksum = value;
                }
            }
        }

        /*********************************************
         * Constructors
         *********************************************/

        //  Main Constructor
        public SipConnection()
        {
            this.HasChecksum = false;
        }

        /// <summary>
        ///     SIP Constructor taking the connection information as string parameters.
        /// </summary>
        /// <param name="ip">SIP server IP</param>
        /// <param name="port">SIP server port</param>
        /// <param name="username">SIP server username</param>
        /// <param name="password">SIP server password</param>
        /// <param name="extra_number">SIP server extra number (optional in some implementations)</param>
        public SipConnection(string ip, string port, string username, string password, string extra_number = "")
        {
            this.domain = ip;
            this.port = port;
            this.username = username;
            this.password = password;
            this.extra_number = extra_number;
            this.HasChecksum = false;
        }
        
        /*********************************************
         * Public Methods
         *********************************************/

        /// <summary>
        ///     Starts SIP connection assuming that SIP parameters were defined in the contructor.
        /// </summary>
        public void Open()
        {
            ipAddress = IPAddress.Parse(this.domain);
            remoteEP = new IPEndPoint(ipAddress, Int32.Parse(this.port));

            //  Set up socket.
            try
            {
                sender.Connect(remoteEP);
            }
            catch (Exception ex)
            {

                throw new NotConnectedException(ex.Message);
            }

            //  Set up SIP start command.
            string sipCommand = string.Format("9300CN{0}|CO{1}|CP{2}", this.username, this.password, this.extra_number);

            //  Communicate with server.
            string response = sipFactory(sipCommand);

            if (response.Contains("96"))
            {
                connected = true;
                throw new NoChecksumException("The server was reached, but the checksum was missing or incorrect.");
            }
            else if (response.Contains("940"))
            {
                connected = false;
                throw new InvalidParameterException("The server was reached, but the log in parameters appear to be incorrect.");
            }
            else if (response.Contains("941"))
            {
                if (HandShake() != "0") connected = true;
                else
                {
                    connected = false;
                    throw new HandshakeFailedException("Unable to establish SIP rules.  Handshake with server failed!");
                }
            }
            else
            {
                connected = false;
                throw new ConnectionFailedException("Unable to connect to the server.  Are your SIP parameters correct?  Is the server available?");
            }
        }
        

        /// <summary>
        ///     Starts connection with SIP server.  Parameters are required.  Use this if you do not establish server parameteres in the constructor.
        /// </summary>
        /// <param name="ip">SIP server IP</param>
        /// <param name="port">SIP server port</param>
        /// <param name="username">SIP server username</param>
        /// <param name="password">SIP server password</param>
        /// <param name="extra_number">SIP server extra number (optional in some implementations)</param>
        public void Open(string ip, string port, string username, string password, string extra_number)
        {
            //  Set up instance variables.   
            this.domain = ip;
            this.port = port;
            this.username = username;
            this.password = password;
            this.extra_number = extra_number;
            Open();
        }

        /// <summary>
        ///     Test whether an instance of a constructed SIP class is able to communicate with the server.  Requires an open connection.
        /// </summary>
        /// <returns>Returns true if connection is viable.</returns>
        public bool TestConnection()
        {
            Open();
            Close();
            return connected;
        }

        /// <summary>
        ///     Test whether an instance of an unconstructed SIP class is able to communicate with the server.  Use this for on-the-fly tests of SIP parameters.
        /// </summary>
        /// <param name="ip">SIP server IP</param>
        /// <param name="port">SIP server port</param>
        /// <param name="username">SIP server username</param>
        /// <param name="password">SIP server password</param>
        /// <param name="extra_number">SIP server extra number (optional in some implementations)</param>
        /// <returns>Returns true if connection is viable.</returns>
        public bool TestConnection(string ip, string port, string username, string password, string extra_number)
        {
            this.domain = ip;
            this.port = port;
            this.username = username;
            this.password = password;
            this.extra_number = extra_number;
            Open();
            Close();
            return connected;
        }
        

        /// <summary>
        ///     Returns a raw dump of an unparsed Patron Response Code query.
        /// </summary>
        /// <param name="barcode">Barcode of the patron.</param>
        /// <returns>Return raw SIP2 dump of patron request message</returns>
        public string RawPatronDump(string barcode)
        {
            if (connected)
            {
                string date = GetDateString();
                string sipCommand = string.Format("63001{0}          AO1|AA{1}|AC{2}|AD|BP|BQ", date, barcode, this.password);
                return sipFactory(sipCommand);
            }
            else
            {
                throw new NotConnectedException("Server is currently unavailable.  Please see a librarian!");
            }
        }

        public Dictionary<string, string> PatronData(string barcode)
        {
            var patronArray = new Dictionary<string, string>();

            Patron patron = new Patron();
            string response = String.Empty;
            if (connected)
            {
                string date = GetDateString();
                string sipCommand = string.Format("63001{0}          AO1|AA{1}|AC{2}|AD|BP|BQ", date, barcode, this.password);
                response = sipFactory(sipCommand);
                patron.PatronParse(response);
                patronArray.Add("name", patron.Name);
                patronArray.Add("email", patron.Email);
                patronArray.Add("phone", patron.Phone);
                patronArray.Add("expiry", patron.Expiry);
                patronArray.Add("auth", patron.Authorized.ToString());
                patronArray.Add("fines", patron.Fines.ToString());
                patronArray.Add("inst", patron.InstitutionId);
                patronArray.Add("message", patron.Message);
                patronArray.Add("type", patron.Type);
                patronArray.Add("address", patron.Address);
                patronArray.Add("home", patron.Home);
                patronArray.Add("HoldCount", patron.HoldCount);
                patronArray.Add("OverdueCount", patron.OverdueCount);
                patronArray.Add("CheckOutCount", patron.CheckOutCount);
                patronArray.Add("FineItemsCount", patron.FinedItemCount);
                patronArray.Add("RecallItemCount", patron.RecallItemCount);
                patronArray.Add("UnavailableHoldCount", patron.UnavailableHoldCount);
                return patronArray;
            }
            else
            {
                throw new NotConnectedException("Server is currently unavailable.  Please see a librarian!");
            }
        }

        public Dictionary<string, string> ItemData(string barcode)
        {
            var itemArray = new Dictionary<string, string>();
            Item item = new Item();
            string response = String.Empty;
            if(connected)
            {
                string date = GetDateString();
                string sipCommand = string.Format("17{0} AO|AB{1}|AC", date, barcode);
                response = sipFactory(sipCommand);
                item.ItemParse(response);
                itemArray.Add("Barcode", barcode);
                itemArray.Add("Title", item.Title);
                itemArray.Add("CallNumber", item.CallNumber);
                itemArray.Add("HomeLocation", item.HomeLocation);
                itemArray.Add("CurrentLocation", item.CurrentLocation);
                itemArray.Add("DueDate", item.DueDate);
                itemArray.Add("MediaType", item.MediaType);
                return itemArray;
            }
            else
            {
                throw new NotConnectedException("Server is currently unavailable.  Please see a librarian!");
            }
        }


        /// <summary>
        ///     Authorizes patron barcode against the SIP server.  This is should be used for especially for public facing implementations of SIP2.
        /// </summary>
        /// <param name="barcode">Barcode of the patron.</param>
        /// <param name="patron">Instance of the patron class.  This will be popluated with a human-readable break down of the Patron Response Message from the SIP server.</param>
        /// <param name="failureResponse">Reason, if any, that a card failed to authorize.</param>
        /// <returns>Return true if authorization is successful.  Returns false if bardcode failed to authorize.</returns>
        public bool AuthorizeBarcode(string barcode, ref Patron patron, ref string failureResponse)
        {
            failureResponse = "";
            patron = new Patron();
            string response = String.Empty;
            if (connected)
            {
                string date = GetDateString();
                string sipCommand = string.Format("63001{0}          AO1|AA{1}|AC{2}|AD|BP|BQ", date, barcode, this.password);
                response = sipFactory(sipCommand);
                patron.PatronParse(response);

                if ((patron.Authorized) & (patron.Fines < patron.FineLimit))
                {
                    if (patron.Pin == "Y")
                        authorized = true;
                    else
                        authorized = false;

                    return authorized;
                }
                else
                {
                    failureResponse = "There seems to be a problem with your card.  Please see a librarian!";
                    return authorized = false;
                }
            }
            else
            {
                throw new NotConnectedException("Server is currently unavailable.  Please see a librarian!");
            }
        }

        //Returns "YES" on successful, no bars
        //If no need for descriptive responses a simple bool would do fine here
        public string AuthorizeBarcode(string barcode, string pin = "")
        {
            string result = "";
            Patron patron = new Patron();
            string response = String.Empty;
            if (connected)
            {
                string date = GetDateString();
                //string sipCommand = string.Format("63001{0}          AO1|AA{1}|AC{2}|AD{3}|BP|BQ", date, barcode, this.password, pin);
                string sipCommand = string.Format("63001{0}          AO1|AA{1}|AD{3}|BP|BQ", date, barcode, this.password, pin);
                response = sipFactory(sipCommand);
                patron.PatronParse(response);

                string expire = patron.Expiry;
                DateTime exdate = DateTime.ParseExact(expire, "yyyyMMdd", CultureInfo.InvariantCulture);


                if ((patron.Pin == "Y"))
                {
                    result = "YES";
                    if (patron.Message != null && patron.Message.ToUpper() == "BLOCKED")
                        result = "BLOCKED";
                    if (patron.Fines > 10)
                        result = "FINES";
                    if (expire != "")
                        if (exdate < DateTime.Now)
                            result = "EXPIRED";
                }
                else
                    result = "NO";

                return result;
            }
            else
            {
                throw new NotConnectedException("Server is currently unavailable.  Please see a librarian!");
            }
        }

      

        public bool PatronHome(string barcode)
        {
            bool result = false;
            Patron patron = new Patron();
            string response = String.Empty;
            if (connected)
            {
                string date = GetDateString();
                string sipCommand = string.Format("63001{0}          AO1|AA{1}|AC{2}|AD|BP|BQ", date, barcode, this.password);
                response = sipFactory(sipCommand);
                patron.PatronParse(response);
                if (patron.Type != "" && patron.Type != null)
                    if (patron.Type.ToUpper().Contains("RECIPROCAL"))
                        result = true;

                return result;
            }
            else
                throw new NotConnectedException("Server is currently unavailable.  Please see a librarian!");
        }

        //Change to dictionary or arrays
        public List<string> AvailPatronHolds(string barcode, string pin = "")
        {
            Patron patron = new Patron();
            string response = String.Empty;
            List<string> avholdItems = new List<string>();
            if (connected)
            {
                List<string> holdItems = PatronItemList("holds", barcode, pin);
                List<string> unholdItems = PatronItemList("unaholds", barcode, pin);
               
                foreach (var h in holdItems)
                {
                    bool match = false;
                    foreach (var u in unholdItems)
                    {
                        if (h == u)
                            match = true;
                    }

                    if (h != "" && match == false)
                        avholdItems.Add(h);
                }
                return avholdItems;
            }
            else
                throw new NotConnectedException("Server is currently unavailable.  Please see a librarian!");
        }

        //Takes in type of call with credentials and queries for a list of items for said patron dependent on type
        public List<string> PatronItemList(string typeOfCall, string barcode, string pin = "")
        {
            Patron patron = new Patron();
            string response = String.Empty;
            string sipType = "";
            List<string> ListedItems = new List<string>();
            List<string> Items = new List<string>();
            if (connected)
            {
                switch (typeOfCall)
                {
                    case "holds":
                        sipType = holdsIdentifier;
                        break;
                    case "overdues":
                        sipType = overdueIdentifier;
                        break;
                    case "charges":
                        sipType = chargedIdentifier;
                        break;
                    case "fines":
                        sipType = finedIdentifier;
                        break;
                    case "recalls":
                        sipType = recallIdentifier;
                        break;
                    case "unaholds":
                        sipType = unaHoldsIdentifier;
                        break;
                }

                string date = GetDateString();
                string sipCommand = string.Format("63001{0}{4}          AO|AA{1}|AD{3}", date, barcode, this.password, pin, sipType);
                response = sipFactory(sipCommand);
                patron.PatronParse(response);
                
                switch (typeOfCall)
                {
                    case "holds":
                        Items = patron.HoldItems;
                        break;
                    case "overdues":
                        Items = patron.OverdueItems;
                        break;
                    case "charges":
                        Items = patron.ChargedItems;
                        break;
                    case "fines":
                        Items = patron.FinedItems;
                        break;
                    case "recalls":
                        Items = patron.RecallItems;
                        break;
                    case "unaholds":
                        Items = patron.UnHoldItems;
                        break;
                }

                foreach (var h in Items)
                    if (h != "")
                        ListedItems.Add(h);

                return ListedItems;
            }
            else
                throw new NotConnectedException("Server is currently unavailable.  Please see a librarian!");
        }

        /// <summary>
        ///     Method to checkout items via SIP2 protocol.  Returns list of Items.  Returns null if patron is not authorized.  
        /// </summary>
        /// <param name="patronBarcode"></param>
        /// <param name="itemBarcodes"></param>
        /// <returns>Returns list of Items.  Returns null if patron is not authorized.</returns>
        public List<Item> CheckOut(string patronBarcode, IEnumerable<string> itemBarcodes)
        {
            if (!connected) throw new NotConnectedException("Cannot checkout books.  SIP connection is not established!");
            if (authorized)
            {
                List<Item> itemListOut = new List<Item>();
                string date = GetDateString();
                foreach (string item in itemBarcodes)
                {
                    Item CheckedOutItem = new Item();
                    string sipCommand = string.Format("11YN{0}                  AO1|AA{1}|AB{2}|AC{3}", date, patronBarcode, item, this.password);
                    string sipResponse = (sipFactory(sipCommand));
                    CheckedOutItem.ItemParse(sipResponse);
                    itemListOut.Add(CheckedOutItem);
                }
                return itemListOut;
            }
            else
            {
                List<Item> itemListOut = null;
                return itemListOut;
            }
        }



        /// <summary>
        ///     Method for checking in materials.  Returns a list of parsed SIP item responses.  Returns null if transaction fails or no barcodes entered.
        /// </summary>
        /// <param name="itemBarcodes">Barcode numbers of the items to be checked in.</param>
        /// <returns>Returns a list of parsed SIP item responses.  Returns null if transaction fails.</returns>
        public List<Item> CheckIn(IEnumerable<string> itemBarcodes)
        {
            if (!connected) throw new NotConnectedException("Cannot checkin books.  SIP connection is not established!");
            List<Item> itemListOut = null;
            string date = GetDateString();
            foreach (string item in itemBarcodes)
            {
                Item CheckedInItem = new Item();
                string sipCommand = string.Format("09Y{0}{1}AP0|AO1|AB{2}|AC{3}", date, date, item, this.password);
                string sipResponse = (sipFactory(sipCommand));
                CheckedInItem.ItemParse(sipResponse);
                itemListOut.Add(CheckedInItem);
            }
            return itemListOut;
        }



        /// <summary>
        ///     Method to appy or remove item holds.  Holds removal does not work if the patron has the same barcode on hold more than once.  
        /// </summary>
        /// <param name="patronBarcode">Patron barcode wishing to place the hold.</param>
        /// <param name="itemBarcode">Barcode of the item they wish to place on hold.</param>
        /// <param name="action">Integer value 1 for placing hold.  Integer value -1 for removing hold.</param>
        /// <returns>Return instance of the Item class.  Returns null if barcode is not authorized first.</returns>
        public Item ItemHold(string patronBarcode, string itemBarcode, int action)
        {
            if (!connected) throw new NotConnectedException("Cannot checkin books.  SIP connection is not established!");
            if (authorized)
            {
                string holdAction = String.Empty;
                switch (action)
                {
                    case 1:
                        holdAction = "+";
                        break;
                    case -1:
                        holdAction = "-";
                        break;
                    default:
                        throw new InvalidParameterException("Unrecognized hold parameter!  Use -1 to delete hold, 1 to add hold!");
                }
                Item itemResponse = new Item();
                string date = GetDateString();
                string sipCommand = string.Format("15{0}{1}|AO1|AA{2}|AB{3}|AC{4}", holdAction, date, patronBarcode, itemBarcode, this.password);
                string sipResponse = (sipFactory(sipCommand));
                itemResponse.ItemParse(sipResponse);
                return itemResponse;
            }
            else return null;
        }



        /// <summary>
        ///     Method to renew a list of books.  DOES NOT WORK EFFECTIVELY!  Due dates cannot be calculated as SIP does not have access to these rules directly.  No idea why this is implemented this way.  Help is appreciated!  Use RenewAll for now.
        /// </summary>
        /// <param name="patronBarcode">Patron barcode</param>
        /// <param name="itemBarcodes">IEnumberable of string - the item(s)' barcode(s) to renew.</param>
        /// <returns>Returns list of the Items class.  Returns null if patron is not authorized.</returns>
        public List<Item> Renew(string patronBarcode, IEnumerable<string> itemBarcodes)
        {
            if (!connected) throw new NotConnectedException("Cannot checkout books.  SIP connection is not established!");
            if (authorized)
            {
                List<Item> itemListOut = new List<Item>();
                string date = GetDateString();
                foreach (string item in itemBarcodes)
                {
                    Item RenewedItem = new Item();
                    string sipCommand = string.Format("29YY{0}{1}AO1|AA{2}|AB{3}|AC{4}", date, date, patronBarcode, item, this.password);
                    string sipResponse = (sipFactory(sipCommand));
                    RenewedItem.ItemParse(sipResponse);
                    itemListOut.Add(RenewedItem);
                }
                return itemListOut;
            }
            else
            {
                return null;
            }
        }



        /// <summary>
        ///     Renews all items a patron currently has checked out.  Doesn't seem to care if the renewal limit has been reached.  
        /// </summary>
        /// <param name="patronBarcode">Patron barcode</param>
        /// <returns>Returns true if command was successful.  Returns false if command fails or renewal was unsuccessful.</returns>
        public bool RenewAll(string patronBarcode)
        {
            string date = GetDateString();
            string sipCommand = string.Format("65{0}AO1|AA{1}|AC{2}", date, patronBarcode, this.password);
            string sipResponse = sipFactory(sipCommand);
            if (sipResponse.Substring(2, 1) == "1") return true;
            else return false;
        }


        /// <summary>
        ///     Ends patron session.
        /// </summary>
        /// <param name="patronBarcode">Patron barcode</param>
        /// <returns>Returns true if session was sucessfully ended - false if it wasn't.</returns>
        public bool EndPatronSession(string patronBarcode)
        {
            string date = GetDateString();
            string sipCommand = string.Format("35{0}AO1|AA{1}|AC{2}", date, patronBarcode, this.password);
            string sipResponse = sipFactory(sipCommand);
            if (sipResponse.Substring(2, 1) == "Y") return true;
            else return false;
        }



        /// <summary>
        ///     Closes SIP connection.
        /// </summary>
        public void Close()
        {
            if ((sender != null) & (sender != null))
            {
                incrementer = 0;
                sender.Shutdown(SocketShutdown.Both);
                sender.Close();
            }
            else throw new NotConnectedException("Cannot close connection.  Connection was not established!");
        }



        /// <summary>
        ///     Disposes SIP connection.
        /// </summary>
        public void Dispose()
        {
            Close();
        }



        /*********************************************
         * Private Methods
         *********************************************/

        private string HandShake()
        {
            try
            {
                string rulesCommand = "9900302.0E";
                return sipFactory(rulesCommand);
            }
            catch (Exception)
            {
                return "0";  //  Highly unlikely that this will fail.  Hence try...catch logic vs. if...then logic.
            }
        }



        private string GetDateString()
        {
            string year = DateTime.Now.Year.ToString("00");
            string month = DateTime.Now.Month.ToString("00");
            string day = DateTime.Now.Day.ToString("00");
            string ZZZZ = "    ";
            string hour = DateTime.Now.Hour.ToString("00");
            string minute = DateTime.Now.Minute.ToString("00");
            string second = DateTime.Now.Second.ToString("00");
            return year + month + day + ZZZZ + hour + minute + second;
        }



        private string SipFactoryNoCheckSum(string sipCommand)
        {
            byte[] bytes = new byte[1024];

            // Encode the data string into a byte array.
            byte[] msg = Encoding.ASCII.GetBytes(sipCommand + '\r');

            // Send the data through the socket.
            int bytesSent = sender.Send(msg);

            // Receive the response from the remote device.
            StringBuilder outputString = new StringBuilder();
            string bit = String.Empty;
            

            sender.Receive(bytes);
            bit = Encoding.ASCII.GetString(bytes);

            while (!bit.Contains("\r"))
            {
              //Found this wasn't meeded as it only responds with 1 item in item checks
                        //for (int i = 0; i <= bit.Length - 1; i++)
                        //{
                        //    if (bit[i] == '\r') { break; }
                        //    if (bit[i] != '\0') { outputString.Append(bit[i]); }
                        //} 
            }
            return bit;
           // return outputString.ToString();
        }



        private string SipFactoryWithCheckSum(string sipCommand)
        {

            byte[] bytes = new byte[1024];

            //  Apply incrementer phrase
            sipCommand = sipCommand + "|AY" + incrementer.ToString() + "AZ";

            // Apply checksum
            string sipCommandWithChecksum = CheckSum.ApplyChecksum(sipCommand);

            // Encode the data string into a byte array.
            byte[] msg = Encoding.ASCII.GetBytes(sipCommandWithChecksum + '\r');

            // Send the data through the socket.
            int bytesSent = sender.Send(msg);

            // Receive the response from the remote device.
            StringBuilder outputString = new StringBuilder();
            string bit = String.Empty;

            //  Receive date, scrub it, and make it pretty.
            while (!bit.Contains("\r"))
            {
                sender.Receive(bytes);
                bit = Encoding.ASCII.GetString(bytes);
                for (int i = 0; i <= bit.Length - 1; i++)
                {
                    if (bit[i] == '\r') { break; }
                    if (bit[i] != '\0') { outputString.Append(bit[i]); }
                }
            }

            incrementer++;
            return outputString.ToString();
        }
    }
}
