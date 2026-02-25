using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Acies_Customization.Models
{
    public class LERatingResponse
    {
        public string amtResult { get; set; }
        public LEIntermediateResults intermediateResults { get; set; }
        public Dictionary<string, string> incorrectMappings { get; set; }
        public string productRateIdentifier { get; set; }
    }

    public class LEIntermediateResults
    {
        public decimal covLE { get; set; }
        public decimal AmtTechnicalPremium_covLE { get; set; }
        public decimal AmtPolicyPremium_covLE { get; set; }

        public decimal AmtTechnicalBrokerComm { get; set; }
        public decimal AmtTechnicalMGAComm { get; set; }
        public decimal AmtTechnicalTax { get; set; }
        public decimal AmtTechnicalPremium_total { get; set; }

        public decimal AmtPolicyBrokerComm { get; set; }
        public decimal AmtPolicyMGAComm { get; set; }
        public decimal AmtPolicyTax { get; set; }
        public decimal AmtPolicyPremium_total { get; set; }
    }
}
