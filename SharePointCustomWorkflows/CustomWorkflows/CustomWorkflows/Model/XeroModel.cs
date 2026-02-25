using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CustomWorkflows.Model
{
    public class XeroModel
    {
        public string PortalName { get; set; }
        public string Type { get; set; }
        public string ReportType { get; set; }
        public string Action { get; set; }
        public string InvoiceId { get; set; }
        public string CreditNoteId { get; set; }
        public string PolicyId { get; set; }
        public ContactModel contact { get; set; }
        public InvoiceModel invoice { get; set; }
    }

    public class ContactModel
    {
        public string Name { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string EmailAddress { get; set; }
        public string AddressType { get; set; }
        public string AddressLine1 { get; set; }
        public string AddressLine2 { get; set; }
        public string City { get; set; }
        public string PostalCode { get; set; }
        public string PhoneNumber { get; set; }
    }

    public class InvoiceModel
    {
        public List<InvoiceLineItem> InvoiceLineItems { set; get; }

        public string TrackingName { get; set; }
        public string PolicyNumber { get; set; }
    }

    public class InvoiceLineItem
    {
        public string Description { get; set; }
        public float Quantity { get; set; }
        public float UnitAmount { get; set; }
        public string AccountCode { get; set; }
    }
}
