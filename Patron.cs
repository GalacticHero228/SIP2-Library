/**************************************************************
 * 
 *  (c) 2014 Mark Lesniak - Nice and Nerdy LLC
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
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SIP2
{
    public class Patron
    {
        public string Name { get; set; }
        public string Type { get; set; }
        public decimal Fines { get; set; }
        public decimal FineLimit { get; set; }
        public string Message { get; set; }
        public int HoldItemLimit { get; set; }
        public string Pin { get; set; }
        public string Address { get; set; }
        public string Phone { get; set; }
        public string Email { get; set; }
        public bool Authorized { get; set; }
        public string InstitutionId { get; set; }
        public string Expiry { get; set; }
        public string Home { get; set; }
        public string HoldCount { get; set; }
        public string OverdueCount { get; set; }
        public string CheckOutCount { get; set; }
        public string FinedItemCount { get; set; }
        public string RecallItemCount { get; set; }
        public string UnavailableHoldCount { get; set; }
        public List<string> HoldItems = new List<string>();
        public List<string> UnHoldItems = new List<string>();
        public List<string> ChargedItems = new List<string>();
        public List<string> FinedItems = new List<string>();
        public List<string> OverdueItems = new List<string>();
        public List<string> RecallItems = new List<string>();

        public void PatronParse(string PatronInformationResponse)
        {
            this.Authorized = true;
            string[] patron_data = PatronInformationResponse.Split(new Char[] { '|' });

            //  Check of 14 character fixed field for potential block information.
            // This worked for Vubis Smart but Always kicks in with Evergreen so noone could be authenticated 
            //if ((patron_data[0].ToUpper().Contains("Y"))) this.Authorized = false;

            //  Loop through the rest of the fields.
            foreach (var element in patron_data)
            {
                if (element.Length >= 2)
                {
                    switch(element.Substring(0, 2).ToUpper())
                    {
                        //Name
                        case "AE":
                            this.Name = element.Substring(2);
                            break;
                        //Message
                        case "AF":
                            this.Message = element.Substring(2);
                            break;
                        //Status counts
                        case "64":
                                int id = element.IndexOf("AO");
                                int lastSpace = element.LastIndexOf(" ") + 7;
                                string info = element.Substring(lastSpace, id - lastSpace);
                                this.HoldCount = info.Substring(0, 4);
                                info = info.Remove(0, 4);
                                this.OverdueCount = info.Substring(0, 4);
                                info = info.Remove(0, 4);
                                this.CheckOutCount = info.Substring(0, 4);
                                info = info.Remove(0, 4);
                                this.FinedItemCount = info.Substring(0, 4);
                                info = info.Remove(0, 4);
                                this.RecallItemCount = info.Substring(0, 4);
                                info = info.Remove(0, 4);
                                this.UnavailableHoldCount = info.Substring(0, 4);
                                info = info.Remove(0, 4);
                                this.InstitutionId = element.Substring(id, element.Length - id);
                            break;
                        //Home LIB
                        case "AQ":
                            this.Home = element.Substring(2);
                            break;
                        //Hold Items
                        case "AS":
                            if (element.ToString() != "") this.HoldItems.Add(element.ToString().Substring(2));
                            break;
                        //Overdue Items
                        case "AT":
                            if (element.ToString() != "") this.OverdueItems.Add(element.ToString().Substring(2));
                            break;
                        //Charged Items
                        case "AU":
                            if (element.ToString() != "") this.ChargedItems.Add(element.ToString().Substring(2));
                            break;
                        //Fined Items
                        case "AV":
                            if (element.ToString() != "") this.FinedItems.Add(element.ToString().Substring(2));
                            break;
                        //Address
                        case "BD":
                            this.Address = element.Substring(2);
                            break;
                        //Email
                        case "BE":
                            this.Email = element.Substring(2);
                            break;
                        //Phone
                        case "BF":
                            this.Phone = element.Substring(2);
                            break;
                        //Authorization
                        case "BL":
                            if (element.Substring(2, 1).ToUpper().Trim().Equals("N")) this.Authorized = false;
                            break;
                        //Charged Items
                        case "BU":
                            if (element.ToString() != "") this.RecallItems.Add(element.ToString().Substring(2));
                            break;
                        //Fines
                        case "BV":
                            this.Fines = Convert.ToDecimal(element.Substring(2));
                            break;
                        //Hold Limit
                        case "BZ":
                            this.HoldItemLimit = Convert.ToInt32(element.Substring(2));
                            break;
                        //Fine Limit
                        case "CC":
                            this.FineLimit = Convert.ToDecimal(element.Substring(2));
                            break;
                        //Unavailable Hold Items
                        case "CD":
                            if (element.ToString() != "") this.UnHoldItems.Add(element.Substring(2));
                            break;
                        //PIN
                        case "CQ":
                            this.Pin = element.Substring(2);
                            break;
                        //Card Expiry
                        case "PA":
                            this.Expiry = element.Substring(2);
                            break;
                        //Customer Category
                        case "PC":
                            this.Type = element.Substring(2);
                            break;
                    }
                }
            }
        }
    }
}
