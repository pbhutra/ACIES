using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Acies_Customization.Models
{
    public class RatingRequest
    {
        // Main object that contains the "productFieldInput" object.
        public ProductFieldInput productFieldInput { get; set; }
    }

    public class ProductFieldInput
    {
        // Fields related to the Recruitment product

        public int isCommIncluded { get; set; } = 1;
        public string HasCoverage_covEL { get; set; } = "false";
        public string HasCoverage_covPL { get; set; } = "false";
        public string HasCoverage_covDN { get; set; } = "false";
        public string HasCoverage_covPI { get; set; } = "false";
        public string HasCoverage_covPBI { get; set; } = "false";


        public string amtPremium_covEL { get; set; } = "0";
        public string amtPremium_covPL { get; set; } = "0";
        public string amtPremium_covDN { get; set; } = "0";
        public string amtPremium_covPI { get; set; } = "0";
        public string amtPremium_covPBI { get; set; } = "0";


        public string covDN_Drivers { get; set; } = "0";
        public string covPBI_Tenants { get; set; } = "0";
        public string covPBI_Contents { get; set; } = "0";
        public string covPBI_Computers { get; set; } = "0";
        public string covPBI_LossOfRevenue { get; set; } = "0";
        public string covPBI_ICOW { get; set; } = "0";
        public string covPBI_PortableEU { get; set; } = "0";
        public string covPBI_PortableUK { get; set; } = "0";
        public string covPBI_PortableWW { get; set; } = "0";


        public string covPI_enumLOI { get; set; } = "0";
        public string covPI_enumLOI_USA { get; set; } = "0";
        public string covPL_enumLOI { get; set; } = "0";


        public string amtWageroll_Clerical { get; set; } = "0";
        public string amtWageroll_Computing { get; set; } = "0";
        public string amtWageroll_Professions { get; set; } = "0";
        public string amtWageroll_Medical { get; set; } = "0";
        public string amtWageroll_LightManual { get; set; } = "0";
        public string amtWageroll_Domiciliary { get; set; } = "0";
        public string amtWageroll_HeavyManual { get; set; } = "0";
        public string amtWageroll_Perm { get; set; } = "0";
        public string amtWageroll_OffshoreClerical { get; set; } = "0";
        public string amtWageroll_OffshoreManual { get; set; } = "0";
        public string amtWageroll_Rail { get; set; } = "0";
        public string amtWageroll_Welders { get; set; } = "0";

        public string turnoverUK_Perm_LFY { get; set; } = "0";
        public string turnoverWorld_Perm_LFY { get; set; } = "0";
        public string turnoverUSA_Perm_LFY { get; set; } = "0";
        public string turnoverUK_Perm_NFY { get; set; } = "0";
        public string turnoverWorld_Perm_NFY { get; set; } = "0";
        public string turnoverUSA_Perm_NFY { get; set; } = "0";
        public string turnoverUK_Temp_LFY { get; set; } = "0";
        public string turnoverWorld_Temp_LFY { get; set; } = "0";
        public string turnoverUSA_Temp_LFY { get; set; } = "0";
        public string turnoverUK_Temp_NFY { get; set; } = "0";
        public string turnoverUSA_Temp_NFY { get; set; } = "0";
        public string turnoverWorld_Temp_NFY { get; set; } = "0";

        public string amtPolicyFee { get; set; } = "0";
        public string amtPolicyTax { get; set; } = "0";

        public string fctPolicyBrokerComm { get; set; } = "0";
        public string fctPolicyMGAComm { get; set; } = "0";
        public string fctPolicyTax { get; set; } = "0";
        public string fctSdc_Clerical { get; set; } = "0";
        public string fctSdc_Computing { get; set; } = "0";
        public string fctSdc_Domiciliary { get; set; } = "0";
        public string fctSdc_HeavyManual { get; set; } = "0";
        public string fctSdc_LightManual { get; set; } = "0";
        public string fctSdc_Medical { get; set; } = "0";
        public string fctSdc_OffshoreClerical { get; set; } = "0";
        public string fctSdc_OffshoreManual { get; set; } = "0";
        public string fctSdc_Professions { get; set; } = "0";
        public string fctSdc_Rail { get; set; } = "0";
        public string fctSdc_Welders { get; set; } = "0";

        //Phase 2
        public string fctDiscount_covEL { get; set; } = "0";
        public string fctDiscount_covPL { get; set; } = "0";
        public string fctDiscount_covPI { get; set; } = "0";
        public string fctDiscount_covDN { get; set; } = "0";
        public string fctDiscount_covPBI { get; set; } = "0";


        public string covPL_fctDiscount_Clerical { get; set; } = "0";
        public string covPL_fctDiscount_Computing { get; set; } = "0";
        public string covPL_fctDiscount_Professions { get; set; } = "0";
        public string covPL_fctDiscount_Medical { get; set; } = "0";
        public string covPL_fctDiscount_LightManual { get; set; } = "0";
        public string covPL_fctDiscount_HeavyManual { get; set; } = "0";
        public string covPL_fctDiscount_OffshoreManual { get; set; } = "0";
        public string covPL_fctDiscount_OffshoreClerical { get; set; } = "0";
        public string covPL_fctDiscount_Rail { get; set; } = "0";
        public string covPL_fctDiscount_Domiciliary { get; set; } = "0";
        public string covPL_fctDiscount_Welders { get; set; } = "0";

        public string covPBI_fctDiscount_Tenants { get; set; } = "0";
        public string covPBI_fctDiscount_Contents { get; set; } = "0";
        public string covPBI_fctDiscount_Computers { get; set; } = "0";

        public string covPBI_fctDiscount_PortableUK { get; set; } = "0";
        public string covPBI_fctDiscount_PortableEU { get; set; } = "0";
        public string covPBI_fctDiscount_PortableWW { get; set; } = "0";

        public string covPBI_fctDiscount_ICOW { get; set; } = "0";
        public string covPBI_fctDiscount_LossOfRevenue { get; set; } = "0";

        public string isManualPolicyFee { get; set; } = "0";
    }
}

