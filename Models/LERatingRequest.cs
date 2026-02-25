using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Acies_Customization.Models
{
    public class LERatingRequest
    {
        // Main object that contains the "productFieldInput" object.
        public LEProductFieldInput productFieldInput { get; set; }
    }

    public class LEProductFieldInput
    {
        // Fields related to the Recruitment product
        public int isCommIncluded { get; set; } = 1;
        public string HasCoverage_covLE { get; set; } = "false";
        public string amtPremium_covLE { get; set; } = "0";
        public string turnoverUSA_Perm_NFY { get; set; } = "0";
        public string turnoverUK_Perm_NFY { get; set; } = "0";
        public string turnoverWorld_Perm_NFY { get; set; } = "0";
        public string turnoverUSA_Temp_NFY { get; set; } = "0";
        public string turnoverUK_Temp_NFY { get; set; } = "0";
        public string turnoverWorld_Temp_NFY { get; set; } = "0";
        public string fctPolicyBrokerComm { get; set; } = "0";
        public string discretionaryPremium { get; set; } = "0";
        public string fctDiscount_covLE { get; set; } = "0";
        public string amtPolicyTax { get; set; } = "0";
        public string fctPolicyMGAComm { get; set; } = "0";
        public string amtPolicyFee { get; set; } = "0";
        public string fctPolicyTax { get; set; } = "0";
        public string isEnhanced { get; set; } = "0";
    }
}
