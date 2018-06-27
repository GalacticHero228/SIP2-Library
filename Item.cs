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
    public class Item
    {
        public string DueDate { get; set; }
        public string Title { get; set; }
        public string ItemBarcode { get; set; }
        public string PatronId { get; set; }
        public string InstitutionId { get; set; }
        public string Message { get; set; }
        public bool SuccessfulTransaction { get; set; }
        public bool SuccessfulRenewal { get; set; }
        public bool MagneticMedia { get; set; }
        public bool Desensitize { get; set; }

        public string HomeLocation { get; set; }
        public string CurrentLocation { get; set; }
        public string CallNumber { get; set; }
        public string MediaType { get; set; }

        public void ItemParse(string ItemResponse)
        {
            string[] item_data = ItemResponse.Split(new Char[] { '|' });
            
            if (item_data[0].Substring(2, 1) == "1") this.SuccessfulTransaction = true;
            else this.SuccessfulTransaction = false;

            if (item_data[0].Substring(3, 1) == "Y") this.SuccessfulRenewal = true;
            else this.SuccessfulRenewal = false;

            if (item_data[0].Substring(4, 1) == "Y") this.MagneticMedia = true;
            else this.MagneticMedia = false;

            if (item_data[0].Substring(5, 1) == "Y") this.Desensitize = true;
            else this.Desensitize = false;

            foreach (string element in item_data)
            {
                switch(element.Substring(0, 2).ToUpper())
                {
                    //Patron ID
                    case "AA":
                        this.PatronId = element.Substring(2);
                        break;
                    //Item Barcode
                    case "AB":
                        this.ItemBarcode = element.Substring(2);
                        break;
                    //Messages
                    case "AF":
                        this.Message = element.Substring(2);
                        break;
                    //Due Date
                    case "AH":
                        this.DueDate = element.Substring(2);
                        break;
                    //Title
                    case "AJ":
                        this.Title = element.Substring(2);
                        break;
                    //Institution
                    case "AO":
                        this.InstitutionId = element.Substring(2);
                        break;
                    //Current location
                    case "AP":
                        this.CurrentLocation = element.Substring(2);
                        break;
                    //Home location
                    case "AQ":
                        this.HomeLocation = element.Substring(2);
                        break;
                    //Media Type
                    case "CK":
                        this.MediaType = element.Substring(2);
                        break;
                    //Call Number
                    case "CS":
                        this.CallNumber = element.Substring(2);
                        break;
                }
            }
        }
    }
}
