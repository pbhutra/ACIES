using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Acies_Customization.Models
{
    public class RatingResponse
    {
        public string amtResult { get; set; }
        public IntermediateResults intermediateResults { get; set; }
        public Dictionary<string, string> incorrectMappings { get; set; }
        public string productRateIdentifier { get; set; }
    }

    public class IntermediateResults
    {
        //EL
        public decimal covEL { get; set; }
        public decimal AmtTechnicalPremium_covEL { get; set; }
        public decimal AmtPolicyPremium_covEL { get; set; }

        //PL
        public decimal covPL { get; set; }
        public decimal AmtTechnicalPremium_covPL { get; set; }
        public decimal AmtPolicyPremium_covPL { get; set; }
        public decimal amtTechnicalPremium_Clerical { get; set; }
        public decimal amtPolicyPremium_Clerical { get; set; }
        public decimal amtTechnicalPremium_Computing { get; set; }
        public decimal amtPolicyPremium_Computing { get; set; }
        public decimal amtTechnicalPremium_Professions { get; set; }
        public decimal amtPolicyPremium_Professions { get; set; }
        public decimal amtTechnicalPremium_Medical { get; set; }
        public decimal amtPolicyPremium_Medical { get; set; }
        public decimal amtTechnicalPremium_LightManual { get; set; }
        public decimal amtPolicyPremium_LightManual { get; set; }
        public decimal amtTechnicalPremium_HeavyManual { get; set; }
        public decimal amtPolicyPremium_HeavyManual { get; set; }
        public decimal amtTechnicalPremium_OffshoreManual { get; set; }
        public decimal amtPolicyPremium_OffshoreManual { get; set; }
        public decimal amtTechnicalPremium_OffshoreClerical { get; set; }
        public decimal amtPolicyPremium_OffshoreClerical { get; set; }
        public decimal amtTechnicalPremium_Rail { get; set; }
        public decimal amtPolicyPremium_Rail { get; set; }
        public decimal amtTechnicalPremium_Welders { get; set; }
        public decimal amtPolicyPremium_Welders { get; set; }

        //public decimal amtPremium_Domiciliary { get; set; }


        //DN
        public decimal covDN { get; set; }
        public decimal AmtTechnicalPremium_covDN { get; set; }
        public decimal AmtPolicyPremium_covDN { get; set; }

        //PI
        public decimal covPI { get; set; }
        public decimal AmtTechnicalPremium_covPI { get; set; }
        public decimal AmtPolicyPremium_covPI { get; set; }

        //PBI
        public decimal covPBI { get; set; }
        public decimal AmtTechnicalPremium_covPBI { get; set; }
        public decimal AmtPolicyPremium_covPBI { get; set; }
        public decimal amtTechnicalPremium_Tenants { get; set; }
        public decimal amtPolicyPremium_Tenants { get; set; }
        public decimal amtTechnicalPremium_Contents { get; set; }
        public decimal amtPolicyPremium_Contents { get; set; }
        public decimal amtTechnicalPremium_Computers { get; set; }
        public decimal amtPolicyPremium_Computers { get; set; }
        public decimal amtTechnicalPremium_PortableUK { get; set; }
        public decimal amtPolicyPremium_PortableUK { get; set; }
        public decimal amtTechnicalPremium_PortableEU { get; set; }
        public decimal amtPolicyPremium_PortableEU { get; set; }
        public decimal amtTechnicalPremium_PortableWW { get; set; }
        public decimal amtPolicyPremium_PortableWW { get; set; }
        public decimal amtTechnicalPremium_LossOfRevenue { get; set; }
        public decimal amtPolicyPremium_LossOfRevenue { get; set; }
        public decimal amtTechnicalPremium_ICOW { get; set; }
        public decimal amtPolicyPremium_ICOW { get; set; }


        //Technical Premium 
        public string AmtTechnicalBrokerComm { get; set; }
        public string AmtTechnicalMGAComm { get; set; }
        public string AmtTechnicalMGUComm        { get; set; }
        public string AmtTechnicalPremiumBT { get; set; }
        public string AmtTechnicalTax { get; set; }
        public string AmtTechnicalPremium_total { get; set; }

        //Policy Premium 
        public string AmtPolicyBrokerComm { get; set; }
        public string AmtPolicyMGAComm { get; set; }
        public string AmtPolicyMGUComm { get; set; }
        public string AmtPolicyPremiumBT { get; set; }
        public string AmtPolicyTax { get; set; }
        public string AmtPolicyPremium_total { get; set; }

        //Fees
        public string AmtTechnicalPremiumFee { get; set; }
        public string AmtPolicyPremiumFee { get; set; }


        //public string AmtPremium_total { get; set; }
        //public string AmtPolicyCoverPremium_total { get; set; }

        //public string AmtPremiumAT { get; set; }
    }
}
