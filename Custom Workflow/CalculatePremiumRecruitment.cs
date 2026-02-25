using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using Microsoft.Xrm.Sdk.Workflow;
using Newtonsoft.Json;
using System;
using System.Activities;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Acies_Customization.Models;
using Acies_Customization.ViewModels;
using System.Net;
using Microsoft.Crm.Sdk.Messages;
using System.Globalization;

namespace Acies_Customization.Custom_Workflow
{
    public class CalculatePremiumRecruitment : CodeActivity
    {
        [Input("Recruitment Quote")]
        [ReferenceTarget("lux_recruitmentquotes")]
        public InArgument<EntityReference> RecruitmentQuote { get; set; }

        [Input("Broker")]
        [ReferenceTarget("account")]
        public InArgument<EntityReference> Broker { get; set; }

        [RequiredArgument]
        [Input("Product")]
        [ReferenceTarget("product")]
        public InArgument<EntityReference> Product { get; set; }

        protected override void Execute(CodeActivityContext executionContext)
        {
            ITracingService tracingService = executionContext.GetExtension<ITracingService>();

            //Create the context
            IWorkflowContext context = executionContext.GetExtension<IWorkflowContext>();
            IOrganizationServiceFactory serviceFactory = executionContext.GetExtension<IOrganizationServiceFactory>();
            IOrganizationService service = serviceFactory.CreateOrganizationService(context.UserId);

            EntityReference recruitmentQuoteref = RecruitmentQuote.Get<EntityReference>(executionContext);
            Entity recruitmentQuote = service.Retrieve("lux_recruitmentquotes", recruitmentQuoteref.Id, new ColumnSet(true));

            string quoteNumber = recruitmentQuote?.GetAttributeValue<string>("lux_name") ?? string.Empty;

            decimal defaultTotalCommission = 32.5M;
            decimal defaultBrokerCommission = 25M;
            decimal defaultAciesComm = 7.5M;

            var inceptionDate = Convert.ToDateTime(recruitmentQuote.FormattedValues["lux_inceptiondate"], System.Globalization.CultureInfo.GetCultureInfo("en-GB").DateTimeFormat);

            var applicationType = recruitmentQuote.GetAttributeValue<OptionSetValue>("lux_applicationtype")?.Value;

            if (applicationType == 972970002) // MTA
            {
                //Using effective date for MTA
                inceptionDate = Convert.ToDateTime(recruitmentQuote.FormattedValues["lux_effectivedate"], System.Globalization.CultureInfo.GetCultureInfo("en-GB").DateTimeFormat);
            }

            tracingService.Trace($"Quote Details: Quote Number - {quoteNumber} Record Id - {recruitmentQuote.Id} Inception Date - {inceptionDate}");

            string sessionKey = null;

            try
            {
                MavenBlueRatingResponseViewModel mavenBlueRatingResponseViewModel = new MavenBlueRatingResponseViewModel();

                //Generate Api Payload
                RatingRequest ratingRequest = CreateApiRequest(executionContext, service, recruitmentQuote, inceptionDate, defaultTotalCommission, defaultBrokerCommission, defaultAciesComm);

                string apiBaseUrl = GetEnvironmentVariableValue(service, "lux_MavenBlueAPIBaseUrl");

                string apiKey = GetEnvironmentVariableValue(service, "lux_MavenBlueAPIKey");

                string genericProductRateVariant = GetEnvironmentVariableValue(service, "lux_MavenBlueGenericProductRateVariant");

                //Generate Api Token
                sessionKey = Task.Run(() => CreateSession(apiBaseUrl, apiKey)).Result;

                // Execute Api
                tracingService.Trace($"Rating Request: {JsonConvert.SerializeObject(ratingRequest)}");

                string apiResponse = Task.Run(() => ExecuteApi(ratingRequest, apiBaseUrl, sessionKey, "prdRecruitment", inceptionDate.ToString("yyyy-MM-dd HH:mm:ss"), DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"), genericProductRateVariant, false)).Result;

                tracingService.Trace($"Rating Response: {apiResponse}");

                RatingResponse ratingResponse = JsonConvert.DeserializeObject<RatingResponse>(apiResponse);

                mavenBlueRatingResponseViewModel.RatingResponse = ratingResponse;


                if (recruitmentQuote.Contains("lux_islegalexpensescoverrequired") && recruitmentQuote.GetAttributeValue<bool>("lux_islegalexpensescoverrequired") == true)
                {
                    //Generate Api Payload
                    LERatingRequest leRatingRequest = CreateLEApiRequest(executionContext, service, recruitmentQuote, inceptionDate, defaultTotalCommission, defaultBrokerCommission, defaultAciesComm);

                    tracingService.Trace($"LE Rating Request: {JsonConvert.SerializeObject(leRatingRequest)}");

                    string leProductRateVariant = GetEnvironmentVariableValue(service, "lux_MavenBlueLEProductRateVariant");

                    string apiResponseLE = Task.Run(() => ExecuteApi(leRatingRequest, apiBaseUrl, sessionKey, "prdRecruitment_LE", inceptionDate.ToString("yyyy-MM-dd HH:mm:ss"), DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"), leProductRateVariant, false)).Result;

                    tracingService.Trace($"LE Rating Response: {apiResponseLE}");

                    LERatingResponse leRatingResponse = JsonConvert.DeserializeObject<LERatingResponse>(apiResponseLE);

                    mavenBlueRatingResponseViewModel.LERatingResponse = leRatingResponse;
                }

                CreateRateLines(executionContext, service, recruitmentQuote, inceptionDate, defaultTotalCommission, defaultBrokerCommission, defaultAciesComm, mavenBlueRatingResponseViewModel);

            }
            catch (Exception ex)
            {
                tracingService.Trace("Running in catch");

                string flowUrl = GetEnvironmentVariableValue(service, "lux_MavenBlueErrorLoggingFlowUrl");

                string recordName = $"{recruitmentQuote.LogicalName} {recruitmentQuote.GetAttributeValue<string>("lux_name")}";

                LogFailureToD365UsingPowerAutomateFlow(service, tracingService, flowUrl, recordName, "MavenBlue platform API", ex.InnerException?.Message ?? ex.Message);

                //LogFailureToD365(service, tracingService, recordName, "MavenBlue platform API", ex.InnerException?.Message ?? ex.Message);

                throw new Exception($"MavenBlue platform API error. Details: {ex.InnerException?.Message ?? ex.Message}");
            }
        }

        private RatingRequest CreateApiRequest(CodeActivityContext executionContext, IOrganizationService service, Entity recruitmentQuote, DateTime inceptionDate, decimal defaultTotalCommission, decimal defaultBrokerCommission, decimal defaultAciesComm)
        {
            try
            {
                RatingRequest ratingRequest = new RatingRequest();
                ratingRequest.productFieldInput = new ProductFieldInput();

                int plLOI = recruitmentQuote.GetAttributeValue<OptionSetValue>("lux_publicliabilitylimitofindemnity").Value;
                Dictionary<int, string> plMapping = new Dictionary<int, string>
                    {
                        { 972970001, "1" },
                        { 972970002, "2" },
                        { 972970003, "5" },
                        { 972970004, "10" }
                    };

                string covPlEnumLOI = plMapping.ContainsKey(plLOI) ? plMapping[plLOI] : string.Empty;
                ratingRequest.productFieldInput.HasCoverage_covPL = "true";
                ratingRequest.productFieldInput.covPL_enumLOI = covPlEnumLOI;

                const int Section_PL = 972970024;
                var plSectionDiscounts = GetSectionDiscountStrings(service, recruitmentQuote.Id);

                if (plSectionDiscounts.TryGetValue(Section_PL, out var plDiscount))
                {
                    ratingRequest.productFieldInput.fctDiscount_covPL = plDiscount;
                }

                //Perm Last
                ratingRequest.productFieldInput.turnoverUK_Perm_LFY = recruitmentQuote.GetAttributeValue<Money>("lux_permanentlastuk").Value.ToString();
                ratingRequest.productFieldInput.turnoverWorld_Perm_LFY = recruitmentQuote.GetAttributeValue<Money>("lux_permanentlastworldwideexcusacanada").Value.ToString();
                ratingRequest.productFieldInput.turnoverUSA_Perm_LFY = recruitmentQuote.GetAttributeValue<Money>("lux_permanentlastusacanada").Value.ToString();

                //Perm Next
                ratingRequest.productFieldInput.turnoverUK_Perm_NFY = recruitmentQuote.GetAttributeValue<Money>("lux_permanentestimateuk").Value.ToString();
                ratingRequest.productFieldInput.turnoverWorld_Perm_NFY = recruitmentQuote.GetAttributeValue<Money>("lux_permanentestimateworldwideexcusacanada").Value.ToString();
                ratingRequest.productFieldInput.turnoverUSA_Perm_NFY = recruitmentQuote.GetAttributeValue<Money>("lux_permanentestimateusacanada").Value.ToString();

                var PermOnly = recruitmentQuote.Attributes.Contains("lux_doesthebusinesssupplypermanentworkersonly") ? recruitmentQuote.FormattedValues["lux_doesthebusinesssupplypermanentworkersonly"] : "No";

                if (PermOnly == "No")
                {
                    //Temp Last
                    ratingRequest.productFieldInput.turnoverUK_Temp_LFY = recruitmentQuote.GetAttributeValue<Money>("lux_temporarylastuk").Value.ToString();
                    ratingRequest.productFieldInput.turnoverWorld_Temp_LFY = recruitmentQuote.GetAttributeValue<Money>("lux_temporarylastworldwideexcusacanada").Value.ToString();
                    ratingRequest.productFieldInput.turnoverUSA_Temp_LFY = recruitmentQuote.GetAttributeValue<Money>("lux_temporarylastusacanada").Value.ToString();

                    //Temp Next
                    ratingRequest.productFieldInput.turnoverUK_Temp_NFY = recruitmentQuote.GetAttributeValue<Money>("lux_temporaryestimateuk").Value.ToString();
                    ratingRequest.productFieldInput.turnoverWorld_Temp_NFY = recruitmentQuote.GetAttributeValue<Money>("lux_temporaryestimateworldwideexcusacanada").Value.ToString();
                    ratingRequest.productFieldInput.turnoverUSA_Temp_NFY = recruitmentQuote.GetAttributeValue<Money>("lux_temporaryestimateusacanada").Value.ToString();

                    var ISSDC = recruitmentQuote.Attributes.Contains("lux_doesthebusinessacceptsupervisiondirection") ? recruitmentQuote.FormattedValues["lux_doesthebusinessacceptsupervisiondirection"] : "No";
                    var ISCL = recruitmentQuote.Attributes.Contains("lux_doesthebusinesseveracceptcontractual") ? recruitmentQuote.FormattedValues["lux_doesthebusinesseveracceptcontractual"] : "No";

                    var CatItem = GetRecruitmentCategoryEntities(service, recruitmentQuote);

                    if (CatItem.Entities.Count > 0)
                    {
                        foreach (var item in CatItem.Entities)
                        {
                            string tradeName = null;

                            var aliasValue = item.GetAttributeValue<AliasedValue>("trade.lux_name");
                            if (aliasValue != null && aliasValue.Value is string name)
                            {
                                tradeName = name;
                            }

                            string totalPayroll = item.GetAttributeValue<Money>("lux_totalpayroll").Value.ToString();
                            decimal sdcPercent = (item.GetAttributeValue<decimal>("lux_ofacceptingsdccontractualliability")) / 100;

                            if (tradeName == "Clerical/Administration/Managerial")
                            {
                                int Section_Clerical = 972970002;
                                ratingRequest.productFieldInput.amtWageroll_Clerical = totalPayroll;
                                if (ISSDC == "Yes" || ISCL == "Yes")
                                {
                                    Section_Clerical = 972970003;
                                    ratingRequest.productFieldInput.fctSdc_Clerical = sdcPercent.ToString();
                                }

                                var sectionClericalDiscounts = GetSectionDiscountStrings(service, recruitmentQuote.Id);
                                if (sectionClericalDiscounts.TryGetValue(Section_Clerical, out var clericalDiscount))
                                {
                                    ratingRequest.productFieldInput.covPL_fctDiscount_Clerical = clericalDiscount;
                                }

                                continue;
                            }

                            if (tradeName == "Computing and IT")
                            {
                                int Section_Computing = 972970004;
                                ratingRequest.productFieldInput.amtWageroll_Computing = totalPayroll;
                                if (ISSDC == "Yes" || ISCL == "Yes")
                                {
                                    Section_Computing = 972970005;
                                    ratingRequest.productFieldInput.fctSdc_Computing = sdcPercent.ToString();
                                }

                                var sectionComputingDiscounts = GetSectionDiscountStrings(service, recruitmentQuote.Id);
                                if (sectionComputingDiscounts.TryGetValue(Section_Computing, out var computingDiscount))
                                {
                                    ratingRequest.productFieldInput.covPL_fctDiscount_Computing = computingDiscount;
                                }
                                continue;
                            }

                            if (tradeName == "Professional/Technical (non-manual)")
                            {
                                int Section_Professions = 972970006;
                                ratingRequest.productFieldInput.amtWageroll_Professions = totalPayroll;
                                if (ISSDC == "Yes" || ISCL == "Yes")
                                {
                                    Section_Professions = 972970007;
                                    ratingRequest.productFieldInput.fctSdc_Professions = sdcPercent.ToString();
                                }

                                var sectionProfessionsDiscounts = GetSectionDiscountStrings(service, recruitmentQuote.Id);
                                if (sectionProfessionsDiscounts.TryGetValue(Section_Professions, out var professionDiscount))
                                {
                                    ratingRequest.productFieldInput.covPL_fctDiscount_Professions = professionDiscount;
                                }
                                continue;
                            }

                            if (tradeName == "Medical/Nursing/Care (non domiciliary)")
                            {
                                int Section_Medical = 972970008;
                                ratingRequest.productFieldInput.amtWageroll_Medical = totalPayroll;
                                if (ISSDC == "Yes" || ISCL == "Yes")
                                {
                                    Section_Medical = 972970009;
                                    ratingRequest.productFieldInput.fctSdc_Medical = sdcPercent.ToString();
                                }

                                var sectionMedicalDiscounts = GetSectionDiscountStrings(service, recruitmentQuote.Id);

                                if (sectionMedicalDiscounts.TryGetValue(Section_Medical, out var medicalDiscount))
                                {
                                    ratingRequest.productFieldInput.covPL_fctDiscount_Medical = medicalDiscount;
                                }
                                continue;
                            }

                            if (tradeName == "Manual (drivers/Warehouse/Light Industrial)")
                            {
                                int Section_LightManual = 972970010;
                                ratingRequest.productFieldInput.amtWageroll_LightManual = totalPayroll;
                                if (ISSDC == "Yes" || ISCL == "Yes")
                                {
                                    Section_LightManual = 972970011;
                                    ratingRequest.productFieldInput.fctSdc_LightManual = sdcPercent.ToString();
                                }

                                var sectionLightManualDiscounts = GetSectionDiscountStrings(service, recruitmentQuote.Id);
                                if (sectionLightManualDiscounts.TryGetValue(Section_LightManual, out var lightManualDiscount))
                                {
                                    ratingRequest.productFieldInput.covPL_fctDiscount_LightManual = lightManualDiscount;
                                }
                                continue;
                            }

                            if (tradeName == "Manual (Construction/Heavy Industrial)")
                            {
                                int Section_HeavyManual = 972970027;
                                ratingRequest.productFieldInput.amtWageroll_HeavyManual = totalPayroll;
                                if (ISSDC == "Yes" || ISCL == "Yes")
                                {
                                    Section_HeavyManual = 972970028;
                                    ratingRequest.productFieldInput.fctSdc_HeavyManual = sdcPercent.ToString();
                                }

                                var sectionHeavyManualDiscounts = GetSectionDiscountStrings(service, recruitmentQuote.Id);
                                if (sectionHeavyManualDiscounts.TryGetValue(Section_HeavyManual, out var heavyManualDiscount))
                                {
                                    ratingRequest.productFieldInput.covPL_fctDiscount_HeavyManual = heavyManualDiscount;
                                }
                                continue;
                            }

                            if (tradeName == "Offshore Manual (e.g. Oil rigs/platforms)")
                            {
                                int Section_OffshoreManual = 972970029;
                                ratingRequest.productFieldInput.amtWageroll_OffshoreManual = totalPayroll;
                                if (ISSDC == "Yes" || ISCL == "Yes")
                                {
                                    Section_OffshoreManual = 972970030;
                                    ratingRequest.productFieldInput.fctSdc_OffshoreManual = sdcPercent.ToString();
                                }

                                var sectionOffshoreManualDiscounts = GetSectionDiscountStrings(service, recruitmentQuote.Id);
                                if (sectionOffshoreManualDiscounts.TryGetValue(Section_OffshoreManual, out var offshoreManualDiscount))
                                {
                                    ratingRequest.productFieldInput.covPL_fctDiscount_OffshoreManual = offshoreManualDiscount;
                                }
                                continue;
                            }

                            if (tradeName == "Offshore Non Manual (e.g. control systems/software engineer")
                            {
                                int Section_OffshoreClerical = 972970031;
                                ratingRequest.productFieldInput.amtWageroll_OffshoreClerical = totalPayroll;
                                if (ISSDC == "Yes" || ISCL == "Yes")
                                {
                                    Section_OffshoreClerical = 972970032;
                                    ratingRequest.productFieldInput.fctSdc_OffshoreClerical = sdcPercent.ToString();
                                }

                                var sectionOffshoreClericalDiscounts = GetSectionDiscountStrings(service, recruitmentQuote.Id);
                                if (sectionOffshoreClericalDiscounts.TryGetValue(Section_OffshoreClerical, out var offshoreClericalDiscount))
                                {
                                    ratingRequest.productFieldInput.covPL_fctDiscount_OffshoreClerical = offshoreClericalDiscount;
                                }
                                continue;
                            }

                            if (tradeName == "Safety Critical Rail Work")
                            {
                                int Section_Rail = 972970033;
                                ratingRequest.productFieldInput.amtWageroll_Rail = totalPayroll;
                                if (ISSDC == "Yes" || ISCL == "Yes")
                                {
                                    Section_Rail = 972970034;
                                    ratingRequest.productFieldInput.fctSdc_Rail = sdcPercent.ToString();
                                }

                                var sectionRailDiscounts = GetSectionDiscountStrings(service, recruitmentQuote.Id);
                                if (sectionRailDiscounts.TryGetValue(Section_Rail, out var railDiscount))
                                {
                                    ratingRequest.productFieldInput.covPL_fctDiscount_Rail = railDiscount;
                                }
                                continue;
                            }

                            if (tradeName == "Solicitors or Financial Services (FCA Regulated Activity)")
                            {
                                int Section_Domiciliary = 972970035;
                                ratingRequest.productFieldInput.amtWageroll_Domiciliary = totalPayroll;
                                if (ISSDC == "Yes" || ISCL == "Yes")
                                {
                                    Section_Domiciliary = 972970036;
                                    ratingRequest.productFieldInput.fctSdc_Domiciliary = sdcPercent.ToString();
                                }

                                var sectionDomiciliaryDiscounts = GetSectionDiscountStrings(service, recruitmentQuote.Id);
                                if (sectionDomiciliaryDiscounts.TryGetValue(Section_Domiciliary, out var domiciliaryDiscount))
                                {
                                    ratingRequest.productFieldInput.covPL_fctDiscount_Domiciliary = domiciliaryDiscount;
                                }
                                continue;
                            }

                            if (tradeName == "Welders/Work involving use of heat")
                            {
                                int Section_Welders = 972970037;
                                ratingRequest.productFieldInput.amtWageroll_Welders = totalPayroll;
                                if (ISSDC == "Yes" || ISCL == "Yes")
                                {
                                    Section_Welders = 972970038;
                                    ratingRequest.productFieldInput.fctSdc_Welders = sdcPercent.ToString();
                                }

                                var sectionWeldersDiscounts = GetSectionDiscountStrings(service, recruitmentQuote.Id);
                                if (sectionWeldersDiscounts.TryGetValue(Section_Welders, out var weldersDiscount))
                                {
                                    ratingRequest.productFieldInput.covPL_fctDiscount_Welders = weldersDiscount;
                                }
                                continue;
                            }
                        }
                    }
                    var IsDriverCover = recruitmentQuote.Attributes.Contains("lux_isdriversnegligencecoverrequired") ? recruitmentQuote.GetAttributeValue<bool>("lux_isdriversnegligencecoverrequired") : false;
                    if (IsDriverCover == true)
                    {
                        ratingRequest.productFieldInput.HasCoverage_covDN = "true";
                        var maxDriver = recruitmentQuote.Attributes.Contains("lux_pleaseadvisethemaximumnumberofdrivers") ? recruitmentQuote.GetAttributeValue<string>("lux_pleaseadvisethemaximumnumberofdrivers") : "";
                        ratingRequest.productFieldInput.covDN_Drivers = maxDriver;

                        const int Section_DN = 972970026;
                        var dnSectionDiscounts = GetSectionDiscountStrings(service, recruitmentQuote.Id);

                        if (dnSectionDiscounts.TryGetValue(Section_DN, out var dnDiscount))
                        {
                            ratingRequest.productFieldInput.fctDiscount_covDN = dnDiscount;
                        }
                    }
                }

                //Mapping for EL
                var ELWageroll = recruitmentQuote.Attributes.Contains("lux_estimatedwagerollofagencysownstaff") ? recruitmentQuote.GetAttributeValue<Money>("lux_estimatedwagerollofagencysownstaff").Value : 0;

                ratingRequest.productFieldInput.HasCoverage_covEL = "true";
                ratingRequest.productFieldInput.amtPremium_covEL = ELWageroll.ToString();
                ratingRequest.productFieldInput.amtWageroll_Perm = ELWageroll.ToString();

                const int Section_EL = 972970012;
                var elSectionDiscounts = GetSectionDiscountStrings(service, recruitmentQuote.Id);

                if (elSectionDiscounts.TryGetValue(Section_EL, out var elDiscount))
                {
                    ratingRequest.productFieldInput.fctDiscount_covEL = elDiscount;
                }

                // Mapping for Professional Indemnity Limit of Indemnity
                var piloiMapping = new Dictionary<int, string>
                    {
                        { 500000, "0.5" },
                        { 1000000, "1" },
                        { 2000000, "2" },
                        { 5000000, "5" },
                        { 10000000, "10" }
                    };

                // Mapping for USA Professional Indemnity Limit of Indemnity
                var piUsaLOIMapping = new Dictionary<int, string>
                    {
                        { 1000000, "1" },
                        { 2000000, "2" },
                        { 5000000, "5" }
                    };

                int PILOI = recruitmentQuote.Attributes.Contains("lux_professionallimitofindemnity")
                    ? Convert.ToInt32(recruitmentQuote.FormattedValues["lux_professionallimitofindemnity"].Replace("£", "").Replace(",", ""))
                    : 0;

                int PIUsaLOI = recruitmentQuote.Attributes.Contains("lux_professionalindemnityusalimitofindemnity")
                    ? Convert.ToInt32(recruitmentQuote.FormattedValues["lux_professionalindemnityusalimitofindemnity"].Replace("£", "").Replace(",", ""))
                    : 0;

                string covPIEnumLOI = piloiMapping.ContainsKey(PILOI) ? piloiMapping[PILOI] : string.Empty;
                string covPIEnumLOI_USA = piUsaLOIMapping.ContainsKey(PIUsaLOI) ? piUsaLOIMapping[PIUsaLOI] : string.Empty;

                ratingRequest.productFieldInput.HasCoverage_covPI = "true";
                ratingRequest.productFieldInput.covPI_enumLOI = covPIEnumLOI;
                ratingRequest.productFieldInput.covPI_enumLOI_USA = covPIEnumLOI_USA;

                const int Section_PI = 972970013;
                var piSectionDiscounts = GetSectionDiscountStrings(service, recruitmentQuote.Id);

                if (piSectionDiscounts.TryGetValue(Section_PI, out var piDiscount))
                {
                    ratingRequest.productFieldInput.fctDiscount_covPI = piDiscount;
                }

                //PBI
                var IsPropertyCover = recruitmentQuote.Attributes.Contains("lux_ispropertycoverrequired") ? recruitmentQuote.GetAttributeValue<bool>("lux_ispropertycoverrequired") : false;
                if (IsPropertyCover == true)
                {
                    var propItems = GetRecruitmentLocationEntities(service, recruitmentQuote).Entities;

                    var BuildingSI = propItems.Sum(x => x.Attributes.Contains("lux_buildingssuminsured") ? x.GetAttributeValue<Money>("lux_buildingssuminsured").Value : 0);
                    var TenantsSI = propItems.Sum(x => x.Attributes.Contains("lux_tenantsimprovementssuminsured") ? x.GetAttributeValue<Money>("lux_tenantsimprovementssuminsured").Value : 0);
                    var OfficeSI = propItems.Sum(x => x.Attributes.Contains("lux_officecontentssuminsured") ? x.GetAttributeValue<Money>("lux_officecontentssuminsured").Value : 0);
                    var ComputerSI = propItems.Sum(x => x.Attributes.Contains("lux_computerselectronicequipmentsuminsured") ? x.GetAttributeValue<Money>("lux_computerselectronicequipmentsuminsured").Value : 0);
                    var ISAllRiskSelected = propItems.Where(x => (x.Attributes.Contains("lux_allriskssuminsuredoption") ? x.GetAttributeValue<bool>("lux_allriskssuminsuredoption") : false) == true);

                    var AllRiskSIUK = 0M;
                    var AllRiskSIEU = 0M;
                    var AllRiskSIWorld = 0M;

                    var AllRiskItems = service.RetrieveMultiple(new FetchExpression(GetAllRiskFetchXml(service, recruitmentQuote))).Entities;


                    if (AllRiskItems.Count > 0)
                    {
                        //UK
                        if (propItems.Any(x => x.Attributes.Contains("lux_geographicalarea") && x.GetAttributeValue<OptionSetValue>("lux_geographicalarea").Value == 972970000))
                        {
                            AllRiskSIUK = AllRiskItems.Where(x => x.Attributes.Contains("ac.lux_geographicalarea") && ((OptionSetValue)((AliasedValue)x["ac.lux_geographicalarea"]).Value).Value == 972970000)
                                .Sum(x => x.Attributes.Contains("lux_suminsured") ? x.GetAttributeValue<Money>("lux_suminsured")?.Value ?? 0 : 0);
                        }

                        //UK and Europe
                        if (propItems.Any(x => x.Attributes.Contains("lux_geographicalarea") && x.GetAttributeValue<OptionSetValue>("lux_geographicalarea").Value == 972970001))
                        {
                            AllRiskSIEU = AllRiskItems.Where(x => x.Attributes.Contains("ac.lux_geographicalarea") && ((OptionSetValue)((AliasedValue)x["ac.lux_geographicalarea"]).Value).Value == 972970001)
                                .Sum(x => x.Attributes.Contains("lux_suminsured") ? x.GetAttributeValue<Money>("lux_suminsured")?.Value ?? 0 : 0);
                        }

                        //Worldwide
                        if (propItems.Any(x => x.Attributes.Contains("lux_geographicalarea") && x.GetAttributeValue<OptionSetValue>("lux_geographicalarea").Value == 972970002))
                        {
                            AllRiskSIWorld = AllRiskItems.Where(x => x.Attributes.Contains("ac.lux_geographicalarea") && ((OptionSetValue)((AliasedValue)x["ac.lux_geographicalarea"]).Value).Value == 972970002)
                                .Sum(x => x.Attributes.Contains("lux_suminsured") ? x.GetAttributeValue<Money>("lux_suminsured")?.Value ?? 0 : 0);
                        }
                    }

                    if (TenantsSI > 0)
                    {
                        ratingRequest.productFieldInput.covPBI_Tenants = TenantsSI.ToString();

                        const int Section_TenantsImprovements = 972970015;
                        var sectionTenantsImprovementsDiscounts = GetSectionDiscountStrings(service, recruitmentQuote.Id);

                        if (sectionTenantsImprovementsDiscounts.TryGetValue(Section_TenantsImprovements, out var tenantsImprovementsDiscount))
                        {
                            ratingRequest.productFieldInput.covPBI_fctDiscount_Tenants = tenantsImprovementsDiscount;
                        }
                    }

                    if (OfficeSI > 0)
                    {
                        ratingRequest.productFieldInput.covPBI_Contents = OfficeSI.ToString();

                        const int Section_OfficeContents = 972970016;
                        var sectionOfficeContentsDiscounts = GetSectionDiscountStrings(service, recruitmentQuote.Id);

                        if (sectionOfficeContentsDiscounts.TryGetValue(Section_OfficeContents, out var officeContentsDiscount))
                        {
                            ratingRequest.productFieldInput.covPBI_fctDiscount_Contents = officeContentsDiscount;
                        }
                    }

                    if (ComputerSI > 0)
                    {
                        ratingRequest.productFieldInput.covPBI_Computers = ComputerSI.ToString();

                        const int Section_ComputersElectronics = 972970017;
                        var sectionComputersElectronicsDiscounts = GetSectionDiscountStrings(service, recruitmentQuote.Id);

                        if (sectionComputersElectronicsDiscounts.TryGetValue(Section_ComputersElectronics, out var computersElectronicsDiscount))
                        {
                            ratingRequest.productFieldInput.covPBI_fctDiscount_Computers = computersElectronicsDiscount;
                        }
                    }

                    if (AllRiskSIUK > 0)
                    {
                        ratingRequest.productFieldInput.covPBI_PortableUK = AllRiskSIUK.ToString();

                        const int Section_AllRisksUK = 972970018;
                        var sectionAllRisksUKDiscounts = GetSectionDiscountStrings(service, recruitmentQuote.Id);

                        if (sectionAllRisksUKDiscounts.TryGetValue(Section_AllRisksUK, out var allRisksUKDiscount))
                        {
                            ratingRequest.productFieldInput.covPBI_fctDiscount_PortableUK = allRisksUKDiscount;
                        }

                    }

                    if (AllRiskSIEU > 0)
                    {
                        ratingRequest.productFieldInput.covPBI_PortableEU = AllRiskSIEU.ToString();

                        const int Section_AllRisksEU = 972970019;
                        var sectionAllRisksEUDiscounts = GetSectionDiscountStrings(service, recruitmentQuote.Id);

                        if (sectionAllRisksEUDiscounts.TryGetValue(Section_AllRisksEU, out var allRisksEUDiscount))
                        {
                            ratingRequest.productFieldInput.covPBI_fctDiscount_PortableEU = allRisksEUDiscount;
                        }
                    }

                    if (AllRiskSIWorld > 0)
                    {
                        ratingRequest.productFieldInput.covPBI_PortableWW = AllRiskSIWorld.ToString();

                        const int Section_AllRisksWW = 972970020;
                        var sectionAllRisksWWDiscounts = GetSectionDiscountStrings(service, recruitmentQuote.Id);

                        if (sectionAllRisksWWDiscounts.TryGetValue(Section_AllRisksWW, out var allRisksWWDiscount))
                        {
                            ratingRequest.productFieldInput.covPBI_fctDiscount_PortableWW = allRisksWWDiscount;
                        }
                    }
                }

                var IsBICover = recruitmentQuote.Attributes.Contains("lux_isbusinessinterruptioncoverrequired") ? recruitmentQuote.GetAttributeValue<bool>("lux_isbusinessinterruptioncoverrequired") : false;
                if (IsBICover == true)
                {
                    int? Basis = recruitmentQuote.Attributes.Contains("lux_pleaseselectthebasisofcover") ? recruitmentQuote.GetAttributeValue<OptionSetValue>("lux_pleaseselectthebasisofcover")?.Value : (int?)null;
                    var BISI = recruitmentQuote.Attributes.Contains("lux_businessinterruptionsuminsured") ? recruitmentQuote.GetAttributeValue<Money>("lux_businessinterruptionsuminsured").Value : 0;

                    if (Basis == 972970001) // LOSS OF GROSS Profit
                    {
                        ratingRequest.productFieldInput.covPBI_LossOfRevenue = BISI.ToString();

                        const int Section_BI_LGR = 972970021;
                        var sectionBILGRDiscounts = GetSectionDiscountStrings(service, recruitmentQuote.Id);

                        if (sectionBILGRDiscounts.TryGetValue(Section_BI_LGR, out var biLGRDiscount))
                        {
                            ratingRequest.productFieldInput.covPBI_fctDiscount_LossOfRevenue = biLGRDiscount;
                        }
                    }
                    else if (Basis == 972970002) // Increased Cost Of Working	
                    {
                        ratingRequest.productFieldInput.covPBI_ICOW = BISI.ToString();

                        const int Section_BI_ICOW = 972970022;
                        var sectionBIICOWDiscounts = GetSectionDiscountStrings(service, recruitmentQuote.Id);

                        if (sectionBIICOWDiscounts.TryGetValue(Section_BI_ICOW, out var biICOWDiscount))
                        {
                            ratingRequest.productFieldInput.covPBI_fctDiscount_ICOW = biICOWDiscount;
                        }
                    }
                }

                if (IsPropertyCover || IsBICover)
                {
                    ratingRequest.productFieldInput.HasCoverage_covPBI = "true";

                    const int Section_PBI = 972970025;
                    var pbiSectionDiscounts = GetSectionDiscountStrings(service, recruitmentQuote.Id);

                    if (piSectionDiscounts.TryGetValue(Section_PBI, out var pbiDiscount))
                    {
                        ratingRequest.productFieldInput.fctDiscount_covPBI = pbiDiscount;
                    }
                }

                CalculateAndSetCommissions(executionContext, service, recruitmentQuote, inceptionDate, defaultTotalCommission, ref defaultBrokerCommission, ref defaultAciesComm);

                ratingRequest.productFieldInput.fctPolicyBrokerComm = (defaultBrokerCommission / 100).ToString();
                ratingRequest.productFieldInput.fctPolicyMGAComm = (defaultAciesComm / 100).ToString();
                ratingRequest.productFieldInput.fctPolicyTax = ((recruitmentQuote.GetAttributeValue<decimal>("lux_technicaltax")) / 100).ToString();

                bool isManualFeesApplied = recruitmentQuote.Contains("lux_isthereamanualpolicyfee") && recruitmentQuote.GetAttributeValue<bool>("lux_isthereamanualpolicyfee");

                if (isManualFeesApplied)
                {
                    ratingRequest.productFieldInput.isManualPolicyFee = "1";

                    if (recruitmentQuote.Contains("lux_policyfeeamount") && recruitmentQuote.GetAttributeValue<Money>("lux_policyfeeamount") != null)
                    {
                        ratingRequest.productFieldInput.amtPolicyFee = recruitmentQuote.GetAttributeValue<Money>("lux_policyfeeamount").Value.ToString();
                    }
                    else
                    {
                        ratingRequest.productFieldInput.amtPolicyFee = "0";
                    }
                }
                else
                {
                    ratingRequest.productFieldInput.isManualPolicyFee = "0";
                    ratingRequest.productFieldInput.amtPolicyFee = "0";
                }

                return ratingRequest;
            }
            catch (Exception ex)
            {
                throw new Exception(ex.Message);
            }
        }

        private LERatingRequest CreateLEApiRequest(CodeActivityContext executionContext, IOrganizationService service, Entity recruitmentQuote, DateTime inceptionDate, decimal defaultTotalCommission, decimal defaultBrokerCommission, decimal defaultAciesComm)
        {
            try
            {
                LERatingRequest leRatingRequest = new LERatingRequest() { productFieldInput = new LEProductFieldInput() };

                var LELOI = 0;
                if (recruitmentQuote.FormattedValues.TryGetValue("lux_legalexpenseslimitofindemnity", out string rawLimit))
                {
                    LELOI = ExtractLimitValue(rawLimit);
                }

                //Perm Next
                leRatingRequest.productFieldInput.turnoverUK_Perm_NFY = recruitmentQuote.GetAttributeValue<Money>("lux_permanentestimateuk").Value.ToString();
                leRatingRequest.productFieldInput.turnoverWorld_Perm_NFY = recruitmentQuote.GetAttributeValue<Money>("lux_permanentestimateworldwideexcusacanada").Value.ToString();
                leRatingRequest.productFieldInput.turnoverUSA_Perm_NFY = recruitmentQuote.GetAttributeValue<Money>("lux_permanentestimateusacanada").Value.ToString();

                var PermOnly = recruitmentQuote.Attributes.Contains("lux_doesthebusinesssupplypermanentworkersonly") ? recruitmentQuote.FormattedValues["lux_doesthebusinesssupplypermanentworkersonly"] : "No";

                if (PermOnly == "No")
                {
                    //Temp Next
                    leRatingRequest.productFieldInput.turnoverUK_Temp_NFY = recruitmentQuote.GetAttributeValue<Money>("lux_temporaryestimateuk").Value.ToString();
                    leRatingRequest.productFieldInput.turnoverWorld_Temp_NFY = recruitmentQuote.GetAttributeValue<Money>("lux_temporaryestimateworldwideexcusacanada").Value.ToString();
                    leRatingRequest.productFieldInput.turnoverUSA_Temp_NFY = recruitmentQuote.GetAttributeValue<Money>("lux_temporaryestimateusacanada").Value.ToString();
                }

                leRatingRequest.productFieldInput.HasCoverage_covLE = "true";
                leRatingRequest.productFieldInput.amtPremium_covLE = LELOI.ToString();

                CalculateAndSetCommissions(executionContext, service, recruitmentQuote, inceptionDate, defaultTotalCommission, ref defaultBrokerCommission, ref defaultAciesComm);

                leRatingRequest.productFieldInput.fctPolicyBrokerComm = (defaultBrokerCommission / 100).ToString();
                leRatingRequest.productFieldInput.fctPolicyMGAComm = (defaultAciesComm / 100).ToString();
                leRatingRequest.productFieldInput.fctPolicyTax = ((recruitmentQuote.GetAttributeValue<decimal>("lux_technicaltax")) / 100).ToString();

                const int Section_LE = 972970023;
                var leSectionDiscounts = GetSectionDiscountStrings(service, recruitmentQuote.Id);

                if (leSectionDiscounts.TryGetValue(Section_LE, out var leDiscount))
                {
                    leRatingRequest.productFieldInput.fctDiscount_covLE = leDiscount;
                }

                var isEnhancedCoverRequired = recruitmentQuote.GetAttributeValue<OptionSetValue>("lux_isenhancedcoverrequiredincludingcontractd")?.Value;

                if (isEnhancedCoverRequired == 972970001)
                {
                    leRatingRequest.productFieldInput.isEnhanced = "1";
                }
                return leRatingRequest;
            }
            catch (Exception ex)
            {
                throw new Exception(ex.Message);
            }
        }

        private static async Task<string> CreateSession(string apiBaseUrl, string apiKey)
        {
            try
            {
                using (var client = new HttpClient())
                {
                    var url = $"{apiBaseUrl}/v1/logon";

                    var requestBody = new Dictionary<string, string>
                    {
                        { "apiKey", apiKey }
                    };

                    var content = new StringContent(JsonConvert.SerializeObject(requestBody), Encoding.UTF8, "application/json");

                    var response = await client.PostAsync(url, content);

                    if (response.StatusCode == HttpStatusCode.Unauthorized)
                    {
                        throw new Exception("Authentication failed: Invalid Mavon Blue API Key.");
                    }


                    string responseContent = await response.Content.ReadAsStringAsync();
                    dynamic tokenResponse = JsonConvert.DeserializeObject(responseContent);

                    if (tokenResponse.hasFunctionalError == true)
                    {
                        throw new Exception($"Token generation failed with status code {response.StatusCode}. Error occurred: {tokenResponse.functionalError}");
                    }

                    if (tokenResponse.hasTechnicalError == true)
                    {
                        throw new Exception($"Token generation failed with status code {response.StatusCode}. Error occurred: {tokenResponse.technicalError}");
                    }

                    if (response.IsSuccessStatusCode)
                    {
                        return tokenResponse.sessionKey;
                    }
                    else
                    {
                        throw new Exception($"Token generation failed with status code {response.StatusCode}. Response: {responseContent}");
                    }
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"{ex.Message}");
            }
        }

        private static async Task<string> ExecuteApi(dynamic ratingRequest, string apiBaseUrl, string sessionKey, string product, string inceptionDate, string ratingCreationDate, string productRateVariant, bool useExternalCodes)
        {
            try
            {
                using (var client = new HttpClient())
                {
                    string url = $"{apiBaseUrl}/v1/execute?product={product}&tsTariff={inceptionDate}&tsReference={ratingCreationDate}&productRateVariant={productRateVariant}&useExternalCodes={useExternalCodes}";

                    client.DefaultRequestHeaders.Add("sessionKey", sessionKey);

                    var jsonBody = JsonConvert.SerializeObject(ratingRequest);
                    var content = new StringContent(jsonBody, Encoding.UTF8, "application/json");

                    var response = await client.PostAsync(url, content);
                    string responseContent = await response.Content.ReadAsStringAsync();
                    dynamic apiResponse = JsonConvert.DeserializeObject(responseContent);

                    if (apiResponse.hasFunctionalError == true)
                    {
                        throw new Exception($"Execute API failed with status code {response.StatusCode}. Error occurred: {apiResponse.functionalError}");
                    }

                    if (apiResponse.hasTechnicalError == true)
                    {
                        throw new Exception($"Execute API failed with status code {response.StatusCode}. Error occurred: {apiResponse.technicalError}");
                    }

                    if (response.IsSuccessStatusCode)
                    {
                        return responseContent;
                    }
                    else
                    {
                        throw new Exception($"Execute API failed with status code {response.StatusCode}");
                    }
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"{ex.Message}");
            }
        }

        private void CreateRateLines(CodeActivityContext executionContext, IOrganizationService service, Entity recruitmentQuote, DateTime inceptionDate, decimal defaultTotalCommission, decimal defaultBrokerCommission, decimal defaultAciesComm, MavenBlueRatingResponseViewModel mavenBlueRatingResponseViewModel)
        {
            try
            {
                var Ratingfetch = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                              <entity name='lux_specialistschemerecruitmentpremuim'>
                                <attribute name='lux_name' />
                                <attribute name='lux_section' />
                                <attribute name='lux_recruitmentquote' />
                                <attribute name='transactioncurrencyid' />
                                <attribute name='lux_specialistschemerecruitmentpremuimid' />
                                <order attribute='lux_name' descending='false' />
                                <filter type='and'>
                                  <condition attribute='statecode' operator='eq' value='0' />
                                  <condition attribute='lux_recruitmentquote' operator='eq' uiname='' uitype='lux_recruitmentquotes' value='{recruitmentQuote.Id}' />
                                </filter>
                              </entity>
                            </fetch>";

                var RateItem = service.RetrieveMultiple(new FetchExpression(Ratingfetch)).Entities;

                var PublicLiabilityPerms = RateItem.FirstOrDefault(x => x.GetAttributeValue<OptionSetValue>("lux_section").Value == 972970001);
                var TempsCAM = RateItem.FirstOrDefault(x => x.GetAttributeValue<OptionSetValue>("lux_section").Value == 972970002);
                var TempsCAMSDC = RateItem.FirstOrDefault(x => x.GetAttributeValue<OptionSetValue>("lux_section").Value == 972970003);
                var TempsCIT = RateItem.FirstOrDefault(x => x.GetAttributeValue<OptionSetValue>("lux_section").Value == 972970004);
                var TempsCITSDC = RateItem.FirstOrDefault(x => x.GetAttributeValue<OptionSetValue>("lux_section").Value == 972970005);
                var TempsPT = RateItem.FirstOrDefault(x => x.GetAttributeValue<OptionSetValue>("lux_section").Value == 972970006);
                var TempsPTSDC = RateItem.FirstOrDefault(x => x.GetAttributeValue<OptionSetValue>("lux_section").Value == 972970007);
                var TempsMNC = RateItem.FirstOrDefault(x => x.GetAttributeValue<OptionSetValue>("lux_section").Value == 972970008);
                var TempsMNCSDC = RateItem.FirstOrDefault(x => x.GetAttributeValue<OptionSetValue>("lux_section").Value == 972970009);
                var TempsDWL = RateItem.FirstOrDefault(x => x.GetAttributeValue<OptionSetValue>("lux_section").Value == 972970010);
                var TempsDWLSDC = RateItem.FirstOrDefault(x => x.GetAttributeValue<OptionSetValue>("lux_section").Value == 972970011);

                var EL = RateItem.FirstOrDefault(x => x.GetAttributeValue<OptionSetValue>("lux_section").Value == 972970012);
                var PI = RateItem.FirstOrDefault(x => x.GetAttributeValue<OptionSetValue>("lux_section").Value == 972970013);
                var Building = RateItem.FirstOrDefault(x => x.GetAttributeValue<OptionSetValue>("lux_section").Value == 972970014);
                var TI = RateItem.FirstOrDefault(x => x.GetAttributeValue<OptionSetValue>("lux_section").Value == 972970015);
                var OC = RateItem.FirstOrDefault(x => x.GetAttributeValue<OptionSetValue>("lux_section").Value == 972970016);
                var CEE = RateItem.FirstOrDefault(x => x.GetAttributeValue<OptionSetValue>("lux_section").Value == 972970017);
                var AllRisksUK = RateItem.FirstOrDefault(x => x.GetAttributeValue<OptionSetValue>("lux_section").Value == 972970018);
                var AllRisksEU = RateItem.FirstOrDefault(x => x.GetAttributeValue<OptionSetValue>("lux_section").Value == 972970019);
                var AllRisksWW = RateItem.FirstOrDefault(x => x.GetAttributeValue<OptionSetValue>("lux_section").Value == 972970020);

                var BILGR = RateItem.FirstOrDefault(x => x.GetAttributeValue<OptionSetValue>("lux_section").Value == 972970021);
                var BIICOW = RateItem.FirstOrDefault(x => x.GetAttributeValue<OptionSetValue>("lux_section").Value == 972970022);
                var LE = RateItem.FirstOrDefault(x => x.GetAttributeValue<OptionSetValue>("lux_section").Value == 972970023);

                var PublicLiability = RateItem.FirstOrDefault(x => x.GetAttributeValue<OptionSetValue>("lux_section").Value == 972970024);
                var PBI = RateItem.FirstOrDefault(x => x.GetAttributeValue<OptionSetValue>("lux_section").Value == 972970025);
                var DN = RateItem.FirstOrDefault(x => x.GetAttributeValue<OptionSetValue>("lux_section").Value == 972970026);

                //new
                var TempsCH = RateItem.FirstOrDefault(x => x.GetAttributeValue<OptionSetValue>("lux_section").Value == 972970027);
                var TempsCHSDC = RateItem.FirstOrDefault(x => x.GetAttributeValue<OptionSetValue>("lux_section").Value == 972970028);
                var TempsOP = RateItem.FirstOrDefault(x => x.GetAttributeValue<OptionSetValue>("lux_section").Value == 972970029);
                var TempsOPSDC = RateItem.FirstOrDefault(x => x.GetAttributeValue<OptionSetValue>("lux_section").Value == 972970030);
                var TempsCS = RateItem.FirstOrDefault(x => x.GetAttributeValue<OptionSetValue>("lux_section").Value == 972970031);
                var TempsCSSDC = RateItem.FirstOrDefault(x => x.GetAttributeValue<OptionSetValue>("lux_section").Value == 972970032);
                var TempsSCRW = RateItem.FirstOrDefault(x => x.GetAttributeValue<OptionSetValue>("lux_section").Value == 972970033);
                var TempsSCRWSDC = RateItem.FirstOrDefault(x => x.GetAttributeValue<OptionSetValue>("lux_section").Value == 972970034);
                var TempsSFS = RateItem.FirstOrDefault(x => x.GetAttributeValue<OptionSetValue>("lux_section").Value == 972970035);
                var TempsSFSSDC = RateItem.FirstOrDefault(x => x.GetAttributeValue<OptionSetValue>("lux_section").Value == 972970036);
                var TempsWW = RateItem.FirstOrDefault(x => x.GetAttributeValue<OptionSetValue>("lux_section").Value == 972970037);
                var TempsWWSDC = RateItem.FirstOrDefault(x => x.GetAttributeValue<OptionSetValue>("lux_section").Value == 972970038);
                var TempsOther = RateItem.FirstOrDefault(x => x.GetAttributeValue<OptionSetValue>("lux_section").Value == 972970039);
                var TempsOtherSDC = RateItem.FirstOrDefault(x => x.GetAttributeValue<OptionSetValue>("lux_section").Value == 972970040);

                var PublicLiabilityPermUK = recruitmentQuote.Attributes.Contains("lux_permanentestimateuk") ? recruitmentQuote.GetAttributeValue<Money>("lux_permanentestimateuk").Value : 0;
                var PublicLiabilityPermWorld = recruitmentQuote.Attributes.Contains("lux_permanentestimateworldwideexcusacanada") ? recruitmentQuote.GetAttributeValue<Money>("lux_permanentestimateworldwideexcusacanada").Value : 0;
                var PublicLiabilityPermUSACanada = recruitmentQuote.Attributes.Contains("lux_permanentestimateusacanada") ? recruitmentQuote.GetAttributeValue<Money>("lux_permanentestimateusacanada").Value : 0;
                var PublicLiabilityPerm = PublicLiabilityPermUK + PublicLiabilityPermWorld + PublicLiabilityPermUSACanada;

                //Public Liability Perms
                UpsertPremiumSection(service, PublicLiabilityPerms, recruitmentQuote, 972970001, "Public Liability Perms", PublicLiabilityPerm, 0, 0, 1);

                decimal totalPl = PublicLiabilityPerm;
                var PermOnly = recruitmentQuote.Attributes.Contains("lux_doesthebusinesssupplypermanentworkersonly") ? recruitmentQuote.FormattedValues["lux_doesthebusinesssupplypermanentworkersonly"] : "No";
                var ISSDC = recruitmentQuote.Attributes.Contains("lux_doesthebusinessacceptsupervisiondirection") ? recruitmentQuote.FormattedValues["lux_doesthebusinessacceptsupervisiondirection"] : "No";
                var ISCL = recruitmentQuote.Attributes.Contains("lux_doesthebusinesseveracceptcontractual") ? recruitmentQuote.FormattedValues["lux_doesthebusinesseveracceptcontractual"] : "No";

                if (PermOnly == "No")
                {
                    var CatItem = GetRecruitmentCategoryEntities(service, recruitmentQuote);

                    if (CatItem.Entities.Count > 0)
                    {
                        foreach (var item in CatItem.Entities)
                        {
                            string tradeName = null;

                            var aliasValue = item.GetAttributeValue<AliasedValue>("trade.lux_name");
                            if (aliasValue != null && aliasValue.Value is string name)
                            {
                                tradeName = name;
                            }

                            if (tradeName == "Clerical/Administration/Managerial")
                            {
                                totalPl += item.GetAttributeValue<Money>("lux_totalpayroll").Value;
                                if (ISSDC == "Yes" || ISCL == "Yes")
                                {
                                    UpsertPremiumSection(
                                        service,
                                        TempsCAMSDC,
                                        recruitmentQuote,
                                        972970003,
                                        "Temps Clerical/Administration/Managerial (SDC figure)",
                                        item.GetAttributeValue<Money>("lux_totalpayroll")?.Value ?? 0,
                                        mavenBlueRatingResponseViewModel.RatingResponse.intermediateResults.amtTechnicalPremium_Clerical,
                                        mavenBlueRatingResponseViewModel.RatingResponse.intermediateResults.amtPolicyPremium_Clerical,
                                        3
                                    );

                                    if (TempsCAM != null)
                                    {
                                        service.Delete("lux_specialistschemerecruitmentpremuim", TempsCAM.Id);
                                    }
                                }
                                else
                                {
                                    UpsertPremiumSection(
                                        service,
                                        TempsCAM,
                                        recruitmentQuote,
                                        972970002,
                                        "Temps Clerical/Administration/Managerial",
                                        item.GetAttributeValue<Money>("lux_totalpayroll")?.Value ?? 0,
                                        mavenBlueRatingResponseViewModel.RatingResponse.intermediateResults.amtTechnicalPremium_Clerical,
                                        mavenBlueRatingResponseViewModel.RatingResponse.intermediateResults.amtPolicyPremium_Clerical,
                                        2
                                    );

                                    if (TempsCAMSDC != null)
                                    {
                                        service.Delete("lux_specialistschemerecruitmentpremuim", TempsCAMSDC.Id);
                                    }
                                }
                            }
                            else if (tradeName == "Computing and IT")
                            {
                                totalPl += item.GetAttributeValue<Money>("lux_totalpayroll").Value;
                                if (ISSDC == "Yes" || ISCL == "Yes")
                                {
                                    UpsertPremiumSection(
                                        service,
                                        TempsCITSDC,
                                        recruitmentQuote,
                                        972970005, // Section: Temps Computing and IT (SDC figure)
                                        "Temps Computing and IT (SDC figure)",
                                        item.GetAttributeValue<Money>("lux_totalpayroll")?.Value ?? 0,
                                        mavenBlueRatingResponseViewModel.RatingResponse.intermediateResults.amtTechnicalPremium_Computing,
                                        mavenBlueRatingResponseViewModel.RatingResponse.intermediateResults.amtPolicyPremium_Computing,
                                        5
                                    );

                                    if (TempsCIT != null)
                                    {
                                        service.Delete("lux_specialistschemerecruitmentpremuim", TempsCIT.Id);
                                    }
                                }
                                else
                                {
                                    UpsertPremiumSection(
                                        service,
                                        TempsCIT,
                                        recruitmentQuote,
                                        972970004, // Section: Temps Computing and IT
                                        "Temps Computing and IT",
                                        item.GetAttributeValue<Money>("lux_totalpayroll")?.Value ?? 0,
                                        mavenBlueRatingResponseViewModel.RatingResponse.intermediateResults.amtTechnicalPremium_Computing,
                                        mavenBlueRatingResponseViewModel.RatingResponse.intermediateResults.amtPolicyPremium_Computing,
                                        4
                                    );

                                    if (TempsCITSDC != null)
                                    {
                                        service.Delete("lux_specialistschemerecruitmentpremuim", TempsCITSDC.Id);
                                    }
                                }
                            }
                            else if (tradeName == "Professional/Technical (non-manual)")
                            {
                                totalPl += item.GetAttributeValue<Money>("lux_totalpayroll").Value;
                                if (ISSDC == "Yes" || ISCL == "Yes")
                                {
                                    UpsertPremiumSection(
                                         service,
                                         TempsPTSDC,
                                         recruitmentQuote,
                                         972970007,
                                         "Temps Professional/Technical (non-manual) (SDC Figure)",
                                         item.GetAttributeValue<Money>("lux_totalpayroll")?.Value ?? 0,
                                         mavenBlueRatingResponseViewModel.RatingResponse.intermediateResults.amtTechnicalPremium_Professions,
                                         mavenBlueRatingResponseViewModel.RatingResponse.intermediateResults.amtPolicyPremium_Professions,
                                         7
                                     );

                                    if (TempsPT != null)
                                    {
                                        service.Delete("lux_specialistschemerecruitmentpremuim", TempsPT.Id);
                                    }
                                }
                                else
                                {
                                    UpsertPremiumSection(
                                        service,
                                        TempsPT,
                                        recruitmentQuote,
                                        972970006,
                                        "Temps Professional/Technical (non-manual)",
                                        item.GetAttributeValue<Money>("lux_totalpayroll")?.Value ?? 0,
                                        mavenBlueRatingResponseViewModel.RatingResponse.intermediateResults.amtTechnicalPremium_Professions,
                                        mavenBlueRatingResponseViewModel.RatingResponse.intermediateResults.amtPolicyPremium_Professions,
                                        6
                                    );

                                    if (TempsPTSDC != null)
                                    {
                                        service.Delete("lux_specialistschemerecruitmentpremuim", TempsPTSDC.Id);
                                    }
                                }
                            }
                            else if (tradeName == "Medical/Nursing/Care (non domiciliary)")
                            {
                                totalPl += item.GetAttributeValue<Money>("lux_totalpayroll").Value;
                                if (ISSDC == "Yes" || ISCL == "Yes")
                                {
                                    UpsertPremiumSection(
                                        service,
                                        TempsMNCSDC,
                                        recruitmentQuote,
                                        972970009,
                                        "Temps Medical/Nursing/Care (non domiciliary) (SDC figure)",
                                        item.GetAttributeValue<Money>("lux_totalpayroll")?.Value ?? 0,
                                        mavenBlueRatingResponseViewModel.RatingResponse.intermediateResults.amtTechnicalPremium_Medical,
                                        mavenBlueRatingResponseViewModel.RatingResponse.intermediateResults.amtPolicyPremium_Medical,
                                        9
                                    );

                                    if (TempsMNC != null)
                                    {
                                        service.Delete("lux_specialistschemerecruitmentpremuim", TempsMNC.Id);
                                    }
                                }
                                else
                                {
                                    UpsertPremiumSection(
                                        service,
                                        TempsMNC,
                                        recruitmentQuote,
                                        972970008,
                                        "Temps Medical/Nursing/Care (non domiciliary)",
                                        item.GetAttributeValue<Money>("lux_totalpayroll")?.Value ?? 0,
                                        mavenBlueRatingResponseViewModel.RatingResponse.intermediateResults.amtTechnicalPremium_Medical,
                                        mavenBlueRatingResponseViewModel.RatingResponse.intermediateResults.amtPolicyPremium_Medical,
                                        8
                                    );

                                    if (TempsMNCSDC != null)
                                    {
                                        service.Delete("lux_specialistschemerecruitmentpremuim", TempsMNCSDC.Id);
                                    }
                                }
                            }
                            else if (tradeName == "Manual (drivers/Warehouse/Light Industrial)")
                            {
                                totalPl += item.GetAttributeValue<Money>("lux_totalpayroll").Value;
                                if (ISSDC == "Yes" || ISCL == "Yes")
                                {
                                    UpsertPremiumSection(
                                        service,
                                        TempsDWLSDC,
                                        recruitmentQuote,
                                        972970011,
                                        "Temps Manual (drivers/Warehouse/Light Industrial) (SDC figure)",
                                        item.GetAttributeValue<Money>("lux_totalpayroll")?.Value ?? 0,
                                        mavenBlueRatingResponseViewModel.RatingResponse.intermediateResults.amtTechnicalPremium_LightManual,
                                        mavenBlueRatingResponseViewModel.RatingResponse.intermediateResults.amtPolicyPremium_LightManual,
                                        11
                                    );

                                    if (TempsDWL != null)
                                    {
                                        service.Delete("lux_specialistschemerecruitmentpremuim", TempsDWL.Id);
                                    }
                                }
                                else
                                {
                                    UpsertPremiumSection(
                                        service,
                                        TempsDWL,
                                        recruitmentQuote,
                                        972970010,
                                        "Temps Manual (drivers/Warehouse/Light Industrial)",
                                        item.GetAttributeValue<Money>("lux_totalpayroll")?.Value ?? 0,
                                        mavenBlueRatingResponseViewModel.RatingResponse.intermediateResults.amtTechnicalPremium_LightManual,
                                        mavenBlueRatingResponseViewModel.RatingResponse.intermediateResults.amtPolicyPremium_LightManual,
                                        10
                                    );

                                    if (TempsDWLSDC != null)
                                    {
                                        service.Delete("lux_specialistschemerecruitmentpremuim", TempsDWLSDC.Id);
                                    }
                                }
                            }
                            else if (tradeName == "Manual (Construction/Heavy Industrial)")
                            {
                                totalPl += item.GetAttributeValue<Money>("lux_totalpayroll").Value;
                                if (ISSDC == "Yes" || ISCL == "Yes")
                                {
                                    UpsertPremiumSection(
                                        service,
                                        TempsCHSDC,
                                        recruitmentQuote,
                                        972970028,
                                        "Temps Manual (Construction/Heavy Industrial)",
                                        item.GetAttributeValue<Money>("lux_totalpayroll")?.Value ?? 0,
                                        mavenBlueRatingResponseViewModel.RatingResponse.intermediateResults.amtTechnicalPremium_HeavyManual,
                                        mavenBlueRatingResponseViewModel.RatingResponse.intermediateResults.amtPolicyPremium_HeavyManual,
                                        13
                                    );

                                    if (TempsCH != null)
                                    {
                                        service.Delete("lux_specialistschemerecruitmentpremuim", TempsCH.Id);
                                    }
                                }
                                else
                                {
                                    UpsertPremiumSection(
                                        service,
                                        TempsCH,
                                        recruitmentQuote,
                                        972970027,
                                        "Temps Manual (Construction/Heavy Industrial)",
                                        item.GetAttributeValue<Money>("lux_totalpayroll")?.Value ?? 0,
                                        mavenBlueRatingResponseViewModel.RatingResponse.intermediateResults.amtTechnicalPremium_HeavyManual,
                                        mavenBlueRatingResponseViewModel.RatingResponse.intermediateResults.amtPolicyPremium_HeavyManual,
                                        12
                                    );

                                    if (TempsCHSDC != null)
                                    {
                                        service.Delete("lux_specialistschemerecruitmentpremuim", TempsCHSDC.Id);
                                    }
                                }
                            }
                            else if (tradeName == "Offshore Manual (e.g. Oil rigs/platforms)")
                            {
                                totalPl += item.GetAttributeValue<Money>("lux_totalpayroll").Value;
                                if (ISSDC == "Yes" || ISCL == "Yes")
                                {
                                    UpsertPremiumSection(
                                        service,
                                        TempsOPSDC,
                                        recruitmentQuote,
                                        972970030,
                                        "Temps Offshore Manual (e.g. Oil rigs/platforms)",
                                        item.GetAttributeValue<Money>("lux_totalpayroll")?.Value ?? 0,
                                        mavenBlueRatingResponseViewModel.RatingResponse.intermediateResults.amtTechnicalPremium_OffshoreManual,
                                        mavenBlueRatingResponseViewModel.RatingResponse.intermediateResults.amtPolicyPremium_OffshoreManual,
                                        15
                                    );

                                    if (TempsOP != null)
                                    {
                                        service.Delete("lux_specialistschemerecruitmentpremuim", TempsOP.Id);
                                    }
                                }
                                else
                                {
                                    UpsertPremiumSection(
                                        service,
                                        TempsOP,
                                        recruitmentQuote,
                                        972970029,
                                        "Temps Offshore Manual (e.g. Oil rigs/platforms)",
                                        item.GetAttributeValue<Money>("lux_totalpayroll")?.Value ?? 0,
                                        mavenBlueRatingResponseViewModel.RatingResponse.intermediateResults.amtTechnicalPremium_OffshoreManual,
                                        mavenBlueRatingResponseViewModel.RatingResponse.intermediateResults.amtPolicyPremium_OffshoreManual,
                                        14
                                    );

                                    if (TempsOPSDC != null)
                                    {
                                        service.Delete("lux_specialistschemerecruitmentpremuim", TempsOPSDC.Id);
                                    }
                                }
                            }
                            else if (tradeName == "Offshore Non Manual (e.g. control systems/software engineer")
                            {
                                totalPl += item.GetAttributeValue<Money>("lux_totalpayroll").Value;
                                if (ISSDC == "Yes" || ISCL == "Yes")
                                {
                                    UpsertPremiumSection(
                                        service,
                                        TempsCSSDC,
                                        recruitmentQuote,
                                        972970032,
                                        "Temps Offshore Non Manual (e.g. control systems/software engineer) (SDC figure)",
                                        item.GetAttributeValue<Money>("lux_totalpayroll")?.Value ?? 0,
                                        mavenBlueRatingResponseViewModel.RatingResponse.intermediateResults.amtTechnicalPremium_OffshoreClerical,
                                        mavenBlueRatingResponseViewModel.RatingResponse.intermediateResults.amtPolicyPremium_OffshoreClerical,
                                        17
                                    );

                                    if (TempsCS != null)
                                    {
                                        service.Delete("lux_specialistschemerecruitmentpremuim", TempsCS.Id);
                                    }
                                }
                                else
                                {
                                    UpsertPremiumSection(
                                        service,
                                        TempsCS,
                                        recruitmentQuote,
                                        972970031,
                                        "Temps Offshore Non Manual (e.g. control systems/software engineer)",
                                        item.GetAttributeValue<Money>("lux_totalpayroll")?.Value ?? 0,
                                        mavenBlueRatingResponseViewModel.RatingResponse.intermediateResults.amtTechnicalPremium_OffshoreClerical,
                                        mavenBlueRatingResponseViewModel.RatingResponse.intermediateResults.amtPolicyPremium_OffshoreClerical,
                                        16
                                    );

                                    if (TempsCSSDC != null)
                                    {
                                        service.Delete("lux_specialistschemerecruitmentpremuim", TempsCSSDC.Id);
                                    }
                                }
                            }
                            else if (tradeName == "Safety Critical Rail Work")
                            {
                                totalPl += item.GetAttributeValue<Money>("lux_totalpayroll").Value;
                                if (ISSDC == "Yes" || ISCL == "Yes")
                                {
                                    UpsertPremiumSection(
                                        service,
                                        TempsSCRWSDC,
                                        recruitmentQuote,
                                        972970034,
                                        "Temps Safety Critical Rail Work (SDC figure)",
                                        item.GetAttributeValue<Money>("lux_totalpayroll")?.Value ?? 0,
                                        mavenBlueRatingResponseViewModel.RatingResponse.intermediateResults.amtTechnicalPremium_Rail,
                                        mavenBlueRatingResponseViewModel.RatingResponse.intermediateResults.amtPolicyPremium_Rail,
                                        19
                                    );

                                    if (TempsSCRW != null)
                                    {
                                        service.Delete("lux_specialistschemerecruitmentpremuim", TempsSCRW.Id);
                                    }
                                }
                                else
                                {
                                    UpsertPremiumSection(
                                        service,
                                        TempsSCRW,
                                        recruitmentQuote,
                                        972970033,
                                        "Temps Safety Critical Rail Work",
                                        item.GetAttributeValue<Money>("lux_totalpayroll")?.Value ?? 0,
                                        mavenBlueRatingResponseViewModel.RatingResponse.intermediateResults.amtTechnicalPremium_Rail,
                                        mavenBlueRatingResponseViewModel.RatingResponse.intermediateResults.amtPolicyPremium_Rail,
                                        18
                                    );

                                    if (TempsSCRWSDC != null)
                                    {
                                        service.Delete("lux_specialistschemerecruitmentpremuim", TempsSCRWSDC.Id);
                                    }
                                }
                            }
                            else if (tradeName == "Solicitors or Financial Services (FCA Regulated Activity)")
                            {
                                totalPl += item.GetAttributeValue<Money>("lux_totalpayroll").Value;
                                if (ISSDC == "Yes" || ISCL == "Yes")
                                {
                                    UpsertPremiumSection(
                                        service,
                                        TempsSFSSDC,
                                        recruitmentQuote,
                                        972970036,
                                        "Temps Solicitors or Financial Services (FCA Regulated Activity) (SDC figure)",
                                        item.GetAttributeValue<Money>("lux_totalpayroll")?.Value ?? 0,
                                        0,
                                        0,
                                        21
                                    );

                                    if (TempsSFS != null)
                                    {
                                        service.Delete("lux_specialistschemerecruitmentpremuim", TempsSFS.Id);
                                    }
                                }
                                else
                                {
                                    UpsertPremiumSection(
                                        service,
                                        TempsSFS,
                                        recruitmentQuote,
                                        972970035,
                                        "Temps Solicitors or Financial Services (FCA Regulated Activity)",
                                        item.GetAttributeValue<Money>("lux_totalpayroll")?.Value ?? 0,
                                        0,
                                        0,
                                        20
                                    );

                                    if (TempsSFSSDC != null)
                                    {
                                        service.Delete("lux_specialistschemerecruitmentpremuim", TempsSFSSDC.Id);
                                    }
                                }
                            }
                            else if (tradeName == "Welders/Work involving use of heat")
                            {
                                totalPl += item.GetAttributeValue<Money>("lux_totalpayroll").Value;
                                if (ISSDC == "Yes" || ISCL == "Yes")
                                {
                                    UpsertPremiumSection(
                                        service,
                                        TempsWWSDC,
                                        recruitmentQuote,
                                        972970038,
                                        "Temps Welders/Work involving use of heat (SDC figure)",
                                        item.GetAttributeValue<Money>("lux_totalpayroll")?.Value ?? 0,
                                        mavenBlueRatingResponseViewModel.RatingResponse.intermediateResults.amtTechnicalPremium_Welders,
                                        mavenBlueRatingResponseViewModel.RatingResponse.intermediateResults.amtPolicyPremium_Welders,
                                        23
                                    );

                                    if (TempsWW != null)
                                    {
                                        service.Delete("lux_specialistschemerecruitmentpremuim", TempsWW.Id);
                                    }
                                }
                                else
                                {
                                    UpsertPremiumSection(
                                        service,
                                        TempsWW,
                                        recruitmentQuote,
                                        972970037,
                                        "Temps Welders/Work involving use of heat",
                                        item.GetAttributeValue<Money>("lux_totalpayroll")?.Value ?? 0,
                                        mavenBlueRatingResponseViewModel.RatingResponse.intermediateResults.amtTechnicalPremium_Welders,
                                        mavenBlueRatingResponseViewModel.RatingResponse.intermediateResults.amtPolicyPremium_Welders,
                                        22
                                    );

                                    if (TempsWWSDC != null)
                                    {
                                        service.Delete("lux_specialistschemerecruitmentpremuim", TempsWWSDC.Id);
                                    }
                                }
                            }
                            else if (tradeName == "Other")
                            {
                                totalPl += item.GetAttributeValue<Money>("lux_totalpayroll").Value;
                                if (ISSDC == "Yes" || ISCL == "Yes")
                                {
                                    UpsertPremiumSection(
                                        service,
                                        TempsOtherSDC,
                                        recruitmentQuote,
                                        972970040,
                                        "Temps Other (SDC figure)",
                                        item.GetAttributeValue<Money>("lux_totalpayroll")?.Value ?? 0,
                                        0,
                                        0,
                                        25
                                    );

                                    if (TempsOther != null)
                                    {
                                        service.Delete("lux_specialistschemerecruitmentpremuim", TempsOther.Id);
                                    }
                                }
                                else
                                {
                                    UpsertPremiumSection(
                                        service,
                                        TempsOther,
                                        recruitmentQuote,
                                        972970039,
                                        "Temps Other",
                                        item.GetAttributeValue<Money>("lux_totalpayroll")?.Value ?? 0,
                                        0,
                                        0,
                                        24
                                    );

                                    if (TempsOtherSDC != null)
                                    {
                                        service.Delete("lux_specialistschemerecruitmentpremuim", TempsOtherSDC.Id);
                                    }
                                }
                            }
                        }
                    }

                    //Trade delete logic
                    List<string> allTrades = new List<string>();
                    QueryExpression qETrades = new QueryExpression("lux_recruitmenttrades")
                    {
                        ColumnSet = new ColumnSet("lux_name")
                    };
                    qETrades.Criteria.AddCondition("statuscode", ConditionOperator.Equal, 1);

                    EntityCollection ecTrades = service.RetrieveMultiple(qETrades);
                    if (ecTrades.Entities.Count > 0)
                    {
                        allTrades = ecTrades.Entities.Select(x => x.GetAttributeValue<string>("lux_name")).ToList();
                    }

                    List<string> catFetchTradeNames = CatItem.Entities.Select(item => ((string)((item.GetAttributeValue<AliasedValue>("trade.lux_name")).Value))).ToList();

                    List<string> tradesToBeDeleted = allTrades.Where(trade => !catFetchTradeNames.Contains(trade)).ToList();

                    if (tradesToBeDeleted.Count > 0)
                    {
                        foreach (var tradeName in tradesToBeDeleted)
                        {
                            if (tradeName == "Clerical/Administration/Managerial")
                            {
                                if (TempsCAM != null)
                                {
                                    service.Delete("lux_specialistschemerecruitmentpremuim", TempsCAM.Id);
                                }

                                if (TempsCAMSDC != null)
                                {
                                    service.Delete("lux_specialistschemerecruitmentpremuim", TempsCAMSDC.Id);
                                }
                            }
                            else if (tradeName == "Computing and IT")
                            {
                                if (TempsCIT != null)
                                {
                                    service.Delete("lux_specialistschemerecruitmentpremuim", TempsCIT.Id);
                                }

                                if (TempsCITSDC != null)
                                {
                                    service.Delete("lux_specialistschemerecruitmentpremuim", TempsCITSDC.Id);
                                }
                            }
                            else if (tradeName == "Professional/Technical (non-manual)")
                            {
                                if (TempsPT != null)
                                {
                                    service.Delete("lux_specialistschemerecruitmentpremuim", TempsPT.Id);
                                }

                                if (TempsPTSDC != null)
                                {
                                    service.Delete("lux_specialistschemerecruitmentpremuim", TempsPTSDC.Id);
                                }
                            }
                            else if (tradeName == "Medical/Nursing/Care (non domiciliary)")
                            {
                                if (TempsMNC != null)
                                {
                                    service.Delete("lux_specialistschemerecruitmentpremuim", TempsMNC.Id);
                                }

                                if (TempsMNCSDC != null)
                                {
                                    service.Delete("lux_specialistschemerecruitmentpremuim", TempsMNCSDC.Id);
                                }
                            }
                            else if (tradeName == "Manual (drivers/Warehouse/Light Industrial)")
                            {
                                if (TempsDWL != null)
                                {
                                    service.Delete("lux_specialistschemerecruitmentpremuim", TempsDWL.Id);
                                }

                                if (TempsDWLSDC != null)
                                {
                                    service.Delete("lux_specialistschemerecruitmentpremuim", TempsDWLSDC.Id);
                                }
                            }
                            else if (tradeName == "Manual (Construction/Heavy Industrial)")
                            {
                                if (TempsCH != null)
                                {
                                    service.Delete("lux_specialistschemerecruitmentpremuim", TempsCH.Id);
                                }

                                if (TempsCHSDC != null)
                                {
                                    service.Delete("lux_specialistschemerecruitmentpremuim", TempsCHSDC.Id);
                                }
                            }
                            else if (tradeName == "Offshore Manual (e.g. Oil rigs/platforms)")
                            {
                                if (TempsOP != null)
                                {
                                    service.Delete("lux_specialistschemerecruitmentpremuim", TempsOP.Id);
                                }

                                if (TempsOPSDC != null)
                                {
                                    service.Delete("lux_specialistschemerecruitmentpremuim", TempsOPSDC.Id);
                                }
                            }
                            else if (tradeName == "Offshore Non Manual (e.g. control systems/software engineer")
                            {
                                if (TempsCS != null)
                                {
                                    service.Delete("lux_specialistschemerecruitmentpremuim", TempsCS.Id);
                                }

                                if (TempsCSSDC != null)
                                {
                                    service.Delete("lux_specialistschemerecruitmentpremuim", TempsCSSDC.Id);
                                }
                            }
                            else if (tradeName == "Safety Critical Rail Work")
                            {
                                if (TempsSCRW != null)
                                {
                                    service.Delete("lux_specialistschemerecruitmentpremuim", TempsSCRW.Id);
                                }

                                if (TempsSCRWSDC != null)
                                {
                                    service.Delete("lux_specialistschemerecruitmentpremuim", TempsSCRWSDC.Id);
                                }
                            }
                            else if (tradeName == "Solicitors or Financial Services (FCA Regulated Activity)")
                            {
                                if (TempsSFS != null)
                                {
                                    service.Delete("lux_specialistschemerecruitmentpremuim", TempsSFS.Id);
                                }

                                if (TempsSFSSDC != null)
                                {
                                    service.Delete("lux_specialistschemerecruitmentpremuim", TempsSFSSDC.Id);
                                }
                            }
                            else if (tradeName == "Welders/Work involving use of heat")
                            {
                                if (TempsWW != null)
                                {
                                    service.Delete("lux_specialistschemerecruitmentpremuim", TempsWW.Id);
                                }

                                if (TempsWWSDC != null)
                                {
                                    service.Delete("lux_specialistschemerecruitmentpremuim", TempsWWSDC.Id);
                                }
                            }
                            else if (tradeName == "Other")
                            {
                                if (TempsOther != null)
                                {
                                    service.Delete("lux_specialistschemerecruitmentpremuim", TempsOther.Id);
                                }

                                if (TempsOtherSDC != null)
                                {
                                    service.Delete("lux_specialistschemerecruitmentpremuim", TempsOtherSDC.Id);
                                }
                            }
                        }
                    }

                    var IsDriverCover = recruitmentQuote.Attributes.Contains("lux_isdriversnegligencecoverrequired") ? recruitmentQuote.GetAttributeValue<bool>("lux_isdriversnegligencecoverrequired") : false;
                    if (IsDriverCover == true)
                    {
                        var maxDriver = recruitmentQuote.Attributes.Contains("lux_pleaseadvisethemaximumnumberofdrivers") ? recruitmentQuote.GetAttributeValue<string>("lux_pleaseadvisethemaximumnumberofdrivers") : "";

                        UpsertPremiumSection(
                            service,
                            DN,
                            recruitmentQuote,
                            972970026,
                            "Drivers Negligence",
                            Convert.ToDecimal(maxDriver),
                            mavenBlueRatingResponseViewModel.RatingResponse.intermediateResults.AmtTechnicalPremium_covDN,
                            mavenBlueRatingResponseViewModel.RatingResponse.intermediateResults.AmtPolicyPremium_covDN,
                            27
                        );
                    }
                    else
                    {
                        if (DN != null)
                        {
                            service.Delete("lux_specialistschemerecruitmentpremuim", DN.Id);
                        }
                    }
                }
                else
                {
                    foreach (var item in RateItem.Where(x => (x.GetAttributeValue<OptionSetValue>("lux_section").Value >= 972970002 && x.GetAttributeValue<OptionSetValue>("lux_section").Value <= 972970011) || (x.GetAttributeValue<OptionSetValue>("lux_section").Value >= 972970027 && x.GetAttributeValue<OptionSetValue>("lux_section").Value <= 972970040)))
                    {
                        service.Delete("lux_specialistschemerecruitmentpremuim", item.Id);
                    }

                    if (DN != null)
                    {
                        service.Delete("lux_specialistschemerecruitmentpremuim", DN.Id);
                    }
                }

                //PL
                UpsertPremiumSection(service, PublicLiability, recruitmentQuote, 972970024, "Public Liability", totalPl,
                    mavenBlueRatingResponseViewModel.RatingResponse.intermediateResults.AmtTechnicalPremium_covPL,
                    mavenBlueRatingResponseViewModel.RatingResponse.intermediateResults.AmtPolicyPremium_covPL,
                    26
                );

                //EL
                var ELWageroll = recruitmentQuote.GetAttributeValue<Money>("lux_estimatedwagerollofagencysownstaff")?.Value ?? 0;
                UpsertPremiumSection(service, EL, recruitmentQuote, 972970012, "Employers Liability", ELWageroll,
                    mavenBlueRatingResponseViewModel.RatingResponse.intermediateResults.AmtTechnicalPremium_covEL,
                    mavenBlueRatingResponseViewModel.RatingResponse.intermediateResults.AmtPolicyPremium_covEL,
                    28
                );

                //PI
                var PILOI = recruitmentQuote.FormattedValues.ContainsKey("lux_professionallimitofindemnity") ? Convert.ToDecimal(recruitmentQuote.FormattedValues["lux_professionallimitofindemnity"].Replace("£", "").Replace(",", "")) : 0;
                UpsertPremiumSection(service, PI, recruitmentQuote, 972970013, "Professional Indemnity", PILOI,
                    mavenBlueRatingResponseViewModel.RatingResponse.intermediateResults.AmtTechnicalPremium_covPI,
                    mavenBlueRatingResponseViewModel.RatingResponse.intermediateResults.AmtPolicyPremium_covPI,
                    29
                );

                decimal totalPBICover = 0;
                var IsPropertyCover = recruitmentQuote.Attributes.Contains("lux_ispropertycoverrequired") ? recruitmentQuote.GetAttributeValue<bool>("lux_ispropertycoverrequired") : false;
                if (IsPropertyCover == true)
                {
                    var propItems = GetRecruitmentLocationEntities(service, recruitmentQuote).Entities;

                    var BuildingSI = propItems.Sum(x => x.Attributes.Contains("lux_buildingssuminsured") ? x.GetAttributeValue<Money>("lux_buildingssuminsured").Value : 0);
                    var TenantsSI = propItems.Sum(x => x.Attributes.Contains("lux_tenantsimprovementssuminsured") ? x.GetAttributeValue<Money>("lux_tenantsimprovementssuminsured").Value : 0);
                    var OfficeSI = propItems.Sum(x => x.Attributes.Contains("lux_officecontentssuminsured") ? x.GetAttributeValue<Money>("lux_officecontentssuminsured").Value : 0);
                    var ComputerSI = propItems.Sum(x => x.Attributes.Contains("lux_computerselectronicequipmentsuminsured") ? x.GetAttributeValue<Money>("lux_computerselectronicequipmentsuminsured").Value : 0);
                    var ISAllRiskSelected = propItems.Where(x => (x.Attributes.Contains("lux_allriskssuminsuredoption") ? x.GetAttributeValue<bool>("lux_allriskssuminsuredoption") : false) == true);

                    var AllRiskSIUK = 0M;
                    var AllRiskSIEU = 0M;
                    var AllRiskSIWorld = 0M;

                    var AllRiskItems = service.RetrieveMultiple(new FetchExpression(GetAllRiskFetchXml(service, recruitmentQuote))).Entities;

                    if (AllRiskItems.Count > 0)
                    {
                        //UK
                        if (propItems.Any(x => x.Attributes.Contains("lux_geographicalarea") && x.GetAttributeValue<OptionSetValue>("lux_geographicalarea").Value == 972970000))
                        {
                            AllRiskSIUK = AllRiskItems.Where(x => x.Attributes.Contains("ac.lux_geographicalarea") && ((OptionSetValue)((AliasedValue)x["ac.lux_geographicalarea"]).Value).Value == 972970000)
                                .Sum(x => x.Attributes.Contains("lux_suminsured") ? x.GetAttributeValue<Money>("lux_suminsured")?.Value ?? 0 : 0);
                        }

                        //UK and Europe
                        if (propItems.Any(x => x.Attributes.Contains("lux_geographicalarea") && x.GetAttributeValue<OptionSetValue>("lux_geographicalarea").Value == 972970001))
                        {
                            AllRiskSIEU = AllRiskItems.Where(x => x.Attributes.Contains("ac.lux_geographicalarea") && ((OptionSetValue)((AliasedValue)x["ac.lux_geographicalarea"]).Value).Value == 972970001)
                                .Sum(x => x.Attributes.Contains("lux_suminsured") ? x.GetAttributeValue<Money>("lux_suminsured")?.Value ?? 0 : 0);
                        }

                        //Worldwide
                        if (propItems.Any(x => x.Attributes.Contains("lux_geographicalarea") && x.GetAttributeValue<OptionSetValue>("lux_geographicalarea").Value == 972970002))
                        {
                            AllRiskSIWorld = AllRiskItems.Where(x => x.Attributes.Contains("ac.lux_geographicalarea") && ((OptionSetValue)((AliasedValue)x["ac.lux_geographicalarea"]).Value).Value == 972970002)
                                .Sum(x => x.Attributes.Contains("lux_suminsured") ? x.GetAttributeValue<Money>("lux_suminsured")?.Value ?? 0 : 0);
                        }
                    }

                    if (BuildingSI > 0)
                    {
                        totalPBICover += BuildingSI;
                        UpsertPremiumSection(
                            service,
                            Building,
                            recruitmentQuote,
                            972970014,
                            "Buildings",
                            BuildingSI,
                            0,
                            0,
                            30
                        );
                    }
                    else
                    {
                        if (Building != null)
                        {
                            service.Delete("lux_specialistschemerecruitmentpremuim", Building.Id);
                        }
                    }

                    if (TenantsSI > 0)
                    {
                        totalPBICover += TenantsSI;
                        UpsertPremiumSection(
                            service,
                            TI,
                            recruitmentQuote,
                            972970015,
                            "Tenants Improvements",
                            TenantsSI,
                            mavenBlueRatingResponseViewModel.RatingResponse.intermediateResults.amtTechnicalPremium_Tenants,
                            mavenBlueRatingResponseViewModel.RatingResponse.intermediateResults.amtPolicyPremium_Tenants,
                            31
                        );
                    }
                    else
                    {
                        if (TI != null)
                        {
                            service.Delete("lux_specialistschemerecruitmentpremuim", TI.Id);
                        }
                    }

                    if (OfficeSI > 0)
                    {
                        totalPBICover += OfficeSI;
                        UpsertPremiumSection(
                            service,
                            OC,
                            recruitmentQuote,
                            972970016,
                            "Office Contents",
                            OfficeSI,
                            mavenBlueRatingResponseViewModel.RatingResponse.intermediateResults.amtTechnicalPremium_Contents,
                            mavenBlueRatingResponseViewModel.RatingResponse.intermediateResults.amtPolicyPremium_Contents,
                            32
                        );
                    }
                    else
                    {
                        if (OC != null)
                        {
                            service.Delete("lux_specialistschemerecruitmentpremuim", OC.Id);
                        }
                    }

                    if (ComputerSI > 0)
                    {
                        totalPBICover += ComputerSI;
                        UpsertPremiumSection(
                            service,
                            CEE,
                            recruitmentQuote,
                            972970017,
                            "Computers & Electronic Equipment",
                            ComputerSI,
                            mavenBlueRatingResponseViewModel.RatingResponse.intermediateResults.amtTechnicalPremium_Computers,
                            mavenBlueRatingResponseViewModel.RatingResponse.intermediateResults.amtPolicyPremium_Computers,
                            33
                        );
                    }
                    else
                    {
                        if (CEE != null)
                        {
                            service.Delete("lux_specialistschemerecruitmentpremuim", CEE.Id);
                        }
                    }

                    if (AllRiskSIUK > 0)
                    {
                        totalPBICover += AllRiskSIUK;
                        UpsertPremiumSection(
                            service,
                            AllRisksUK,
                            recruitmentQuote,
                            972970018,
                            "All Risks UK",
                            AllRiskSIUK,
                            mavenBlueRatingResponseViewModel.RatingResponse.intermediateResults.amtTechnicalPremium_PortableUK,
                            mavenBlueRatingResponseViewModel.RatingResponse.intermediateResults.amtPolicyPremium_PortableUK,
                            34
                        );
                    }
                    else
                    {
                        if (AllRisksUK != null)
                        {
                            service.Delete("lux_specialistschemerecruitmentpremuim", AllRisksUK.Id);
                        }
                    }

                    if (AllRiskSIEU > 0)
                    {
                        totalPBICover += AllRiskSIEU;
                        UpsertPremiumSection(
                            service,
                            AllRisksEU,
                            recruitmentQuote,
                            972970019,
                            "All Risks EU & UK",
                            AllRiskSIEU,
                            mavenBlueRatingResponseViewModel.RatingResponse.intermediateResults.amtTechnicalPremium_PortableEU,
                            mavenBlueRatingResponseViewModel.RatingResponse.intermediateResults.amtPolicyPremium_PortableEU,
                            35
                        );
                    }
                    else
                    {
                        if (AllRisksEU != null)
                        {
                            service.Delete("lux_specialistschemerecruitmentpremuim", AllRisksEU.Id);
                        }
                    }

                    if (AllRiskSIWorld > 0)
                    {
                        totalPBICover += AllRiskSIWorld;
                        UpsertPremiumSection(
                            service,
                            AllRisksWW,
                            recruitmentQuote,
                            972970020,
                            "All Risks WW",
                            AllRiskSIWorld,
                            mavenBlueRatingResponseViewModel.RatingResponse.intermediateResults.amtTechnicalPremium_PortableWW,
                            mavenBlueRatingResponseViewModel.RatingResponse.intermediateResults.amtPolicyPremium_PortableWW,
                            36
                        );
                    }
                    else
                    {
                        if (AllRisksWW != null)
                        {
                            service.Delete("lux_specialistschemerecruitmentpremuim", AllRisksWW.Id);
                        }
                    }
                }
                else
                {
                    foreach (var item in RateItem.Where(x => x.GetAttributeValue<OptionSetValue>("lux_section").Value >= 972970014 && x.GetAttributeValue<OptionSetValue>("lux_section").Value <= 972970020))
                    {
                        service.Delete("lux_specialistschemerecruitmentpremuim", item.Id);
                    }
                }

                var IsBICover = recruitmentQuote.Attributes.Contains("lux_isbusinessinterruptioncoverrequired") ? recruitmentQuote.GetAttributeValue<bool>("lux_isbusinessinterruptioncoverrequired") : false;
                if (IsBICover == true)
                {
                    int? Basis = recruitmentQuote.Attributes.Contains("lux_pleaseselectthebasisofcover") ? recruitmentQuote.GetAttributeValue<OptionSetValue>("lux_pleaseselectthebasisofcover")?.Value : (int?)null;
                    var BISI = recruitmentQuote.Attributes.Contains("lux_businessinterruptionsuminsured") ? recruitmentQuote.GetAttributeValue<Money>("lux_businessinterruptionsuminsured").Value : 0;
                    totalPBICover += BISI;

                    if (Basis == 972970001) // LOSS OF REVENUE
                    {
                        UpsertPremiumSection(
                            service,
                            BILGR,
                            recruitmentQuote,
                            972970021,
                            "Business Interruption LGR",
                            BISI,
                            mavenBlueRatingResponseViewModel.RatingResponse.intermediateResults.amtTechnicalPremium_LossOfRevenue,
                            mavenBlueRatingResponseViewModel.RatingResponse.intermediateResults.amtPolicyPremium_LossOfRevenue,
                            37
                        );

                        if (BIICOW != null)
                        {
                            service.Delete("lux_specialistschemerecruitmentpremuim", BIICOW.Id);
                        }
                    }
                    else if (Basis == 972970002) // Increased Cost Of Working	
                    {
                        UpsertPremiumSection(
                            service,
                            BIICOW,
                            recruitmentQuote,
                            972970022,
                            "Business Interruption ICOW",
                            BISI,
                            mavenBlueRatingResponseViewModel.RatingResponse.intermediateResults.amtTechnicalPremium_ICOW,
                            mavenBlueRatingResponseViewModel.RatingResponse.intermediateResults.amtPolicyPremium_ICOW,
                            38
                        );

                        if (BILGR != null)
                        {
                            service.Delete("lux_specialistschemerecruitmentpremuim", BILGR.Id);
                        }
                    }
                }
                else
                {
                    foreach (var item in RateItem.Where(x => x.GetAttributeValue<OptionSetValue>("lux_section").Value >= 972970021 && x.GetAttributeValue<OptionSetValue>("lux_section").Value <= 972970022))
                    {
                        service.Delete("lux_specialistschemerecruitmentpremuim", item.Id);
                    }
                }

                if (IsPropertyCover == true || IsBICover == true)
                {
                    UpsertPremiumSection(
                        service,
                        PBI,
                        recruitmentQuote,
                        972970025,
                        "Property & Business Interruption",
                        totalPBICover,
                        mavenBlueRatingResponseViewModel.RatingResponse.intermediateResults.AmtTechnicalPremium_covPBI,
                        mavenBlueRatingResponseViewModel.RatingResponse.intermediateResults.AmtPolicyPremium_covPBI,
                        39
                    );
                }
                else
                {
                    if (PBI != null)
                    {
                        service.Delete("lux_specialistschemerecruitmentpremuim", PBI.Id);
                    }
                }


                var IsLECover = recruitmentQuote.Attributes.Contains("lux_islegalexpensescoverrequired") ? recruitmentQuote.GetAttributeValue<bool>("lux_islegalexpensescoverrequired") : false;
                if (IsLECover == true)
                {
                    var LELOI = 0;
                    if (recruitmentQuote.FormattedValues.TryGetValue("lux_legalexpenseslimitofindemnity", out string rawLimit))
                    {
                        LELOI = ExtractLimitValue(rawLimit);
                    }

                    UpsertPremiumSection(
                        service,
                        LE,
                        recruitmentQuote,
                        972970023,
                        "Legal Expenses",
                        LELOI,
                        mavenBlueRatingResponseViewModel.LERatingResponse.intermediateResults.AmtTechnicalPremium_covLE,
                        mavenBlueRatingResponseViewModel.LERatingResponse.intermediateResults.AmtPolicyPremium_covLE,
                        40
                    );
                }
                else
                {
                    if (LE != null)
                    {
                        service.Delete("lux_specialistschemerecruitmentpremuim", LE.Id);
                    }
                }

                var FinalRatingfetch = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                    <entity name='lux_specialistschemerecruitmentpremuim'>
                    <attribute name='lux_name' />
                    <attribute name='lux_section' />
                    <attribute name='lux_recruitmentquote' />
                    <attribute name='lux_technicalpremium' />
                    <attribute name='lux_policypremium' />
                    <attribute name='transactioncurrencyid' />
                    <attribute name='lux_specialistschemerecruitmentpremuimid' />
                    <order attribute='lux_name' descending='false' />
                    <filter type='and'>
                    <condition attribute='statecode' operator='eq' value='0' />
                    <condition attribute='lux_recruitmentquote' operator='eq' uiname='' uitype='lux_recruitmentquotes' value='{recruitmentQuote.Id}' />
                    <condition attribute='lux_section' operator='in'>
                    <value>972970012</value>
                    <value>972970013</value>
                    <value>972970023</value>
                    <value>972970024</value>
                    <value>972970025</value>
                    <value>972970026</value>
                    </condition>
                    </filter>
                    </entity>
                    </fetch>";

                var recruitmentList = service.RetrieveMultiple(new FetchExpression(FinalRatingfetch));
                if (recruitmentList.Entities.Count() > 0)
                {
                    CalculateAndSetCommissions(executionContext, service, recruitmentQuote, inceptionDate, defaultTotalCommission, ref defaultBrokerCommission, ref defaultAciesComm);

                    Entity application = service.Retrieve("lux_recruitmentquotes", recruitmentQuote.Id, new ColumnSet("lux_policybrokercommission", "lux_policyaciescommission"));
                    application["lux_technicalbrokercommission"] = defaultBrokerCommission;
                    application["lux_technicalaciescommission"] = defaultAciesComm;
                    application["lux_policybrokercommission"] = defaultBrokerCommission;
                    application["lux_policyaciescommission"] = defaultAciesComm;

                    //Technical Premium
                    decimal AmtTechnicalBrokerCommission = Convert.ToDecimal(mavenBlueRatingResponseViewModel.RatingResponse.intermediateResults.AmtTechnicalBrokerComm);
                    decimal AmtTechnicalAciesCommission = Convert.ToDecimal(mavenBlueRatingResponseViewModel.RatingResponse.intermediateResults.AmtTechnicalMGAComm);
                    decimal AmtTechnicalTotalCommission = AmtTechnicalBrokerCommission + AmtTechnicalAciesCommission;
                    decimal AmtTechnicalPremiumBT = Convert.ToDecimal(mavenBlueRatingResponseViewModel.RatingResponse.intermediateResults.AmtTechnicalPremiumBT);
                    decimal AmtTechnicalTax = Convert.ToDecimal(mavenBlueRatingResponseViewModel.RatingResponse.intermediateResults.AmtTechnicalTax);
                    decimal AmtTechnicalPremiumTotal = Convert.ToDecimal(mavenBlueRatingResponseViewModel.RatingResponse.intermediateResults.AmtTechnicalPremium_total);

                    //Technical Premium Legal
                    decimal AmtTechnicalBrokerCommissionLe = Convert.ToDecimal(mavenBlueRatingResponseViewModel.RatingResponse.intermediateResults.AmtTechnicalBrokerComm) + Convert.ToDecimal(mavenBlueRatingResponseViewModel.LERatingResponse != null ? mavenBlueRatingResponseViewModel.LERatingResponse.intermediateResults.AmtTechnicalBrokerComm : 0);
                    decimal AmtTechnicalAciesCommissionLe = Convert.ToDecimal(mavenBlueRatingResponseViewModel.RatingResponse.intermediateResults.AmtTechnicalMGAComm) + Convert.ToDecimal(mavenBlueRatingResponseViewModel.LERatingResponse != null ? mavenBlueRatingResponseViewModel.LERatingResponse.intermediateResults.AmtTechnicalMGAComm : 0);
                    decimal AmtTechnicalTotalCommissionLe = AmtTechnicalBrokerCommissionLe + AmtTechnicalAciesCommissionLe;
                    decimal AmtTechnicalPremiumBTLe = mavenBlueRatingResponseViewModel.LERatingResponse != null ? Convert.ToDecimal(mavenBlueRatingResponseViewModel.LERatingResponse.intermediateResults.AmtTechnicalPremium_covLE) : 0;
                    decimal AmtTechnicalTaxLe = Convert.ToDecimal(mavenBlueRatingResponseViewModel.RatingResponse.intermediateResults.AmtTechnicalTax) + Convert.ToDecimal(mavenBlueRatingResponseViewModel.LERatingResponse != null ? mavenBlueRatingResponseViewModel.LERatingResponse.intermediateResults.AmtTechnicalTax : 0);
                    decimal AmtTechnicalPremiumTotalLe = Convert.ToDecimal(mavenBlueRatingResponseViewModel.RatingResponse.intermediateResults.AmtTechnicalPremium_total) + Convert.ToDecimal(mavenBlueRatingResponseViewModel.LERatingResponse != null ? mavenBlueRatingResponseViewModel.LERatingResponse.intermediateResults.AmtTechnicalPremium_total : 0);

                    //Policy Premium
                    decimal AmtPolicyBrokerCommission = Convert.ToDecimal(mavenBlueRatingResponseViewModel.RatingResponse.intermediateResults.AmtPolicyBrokerComm);
                    decimal AmtPolicyAciesCommission = Convert.ToDecimal(mavenBlueRatingResponseViewModel.RatingResponse.intermediateResults.AmtPolicyMGAComm);
                    decimal AmtPolicyTotalCommission = AmtPolicyBrokerCommission + AmtPolicyAciesCommission;
                    decimal AmtPolicyPremiumBT = Convert.ToDecimal(mavenBlueRatingResponseViewModel.RatingResponse.intermediateResults.AmtPolicyPremiumBT);
                    decimal AmtPolicyTax = Convert.ToDecimal(mavenBlueRatingResponseViewModel.RatingResponse.intermediateResults.AmtPolicyTax);
                    decimal AmtPolicyPremiumTotal = Convert.ToDecimal(mavenBlueRatingResponseViewModel.RatingResponse.intermediateResults.AmtPolicyPremium_total);

                    //Policy Premium Legal
                    decimal AmtPolicyBrokerCommissionLe = Convert.ToDecimal(mavenBlueRatingResponseViewModel.RatingResponse.intermediateResults.AmtPolicyBrokerComm) + Convert.ToDecimal(mavenBlueRatingResponseViewModel.LERatingResponse != null ? mavenBlueRatingResponseViewModel.LERatingResponse.intermediateResults.AmtPolicyBrokerComm : 0);
                    decimal AmtPolicyAciesCommissionLe = Convert.ToDecimal(mavenBlueRatingResponseViewModel.RatingResponse.intermediateResults.AmtPolicyMGAComm) + Convert.ToDecimal(mavenBlueRatingResponseViewModel.LERatingResponse != null ? mavenBlueRatingResponseViewModel.LERatingResponse.intermediateResults.AmtPolicyMGAComm : 0);
                    decimal AmtPolicyTotalCommissionLe = AmtPolicyBrokerCommissionLe + AmtPolicyAciesCommissionLe;
                    decimal AmtPolicyPremiumBTLe = mavenBlueRatingResponseViewModel.LERatingResponse != null ? Convert.ToDecimal(mavenBlueRatingResponseViewModel.LERatingResponse.intermediateResults.AmtPolicyPremium_covLE) : 0;
                    decimal AmtPolicyTaxLe = Convert.ToDecimal(mavenBlueRatingResponseViewModel.RatingResponse.intermediateResults.AmtPolicyTax) + Convert.ToDecimal(mavenBlueRatingResponseViewModel.LERatingResponse != null ? mavenBlueRatingResponseViewModel.LERatingResponse.intermediateResults.AmtPolicyTax : 0);
                    decimal AmtPolicyPremiumTotalLe = Convert.ToDecimal(mavenBlueRatingResponseViewModel.RatingResponse.intermediateResults.AmtPolicyPremium_total) + Convert.ToDecimal(mavenBlueRatingResponseViewModel.LERatingResponse != null ? mavenBlueRatingResponseViewModel.LERatingResponse.intermediateResults.AmtPolicyPremium_total : 0);

                    //Technical exc Legal
                    application["lux_techbrokercommexclegal_manual"] = new Money(AmtTechnicalBrokerCommission);
                    application["lux_techaciescommexclegal_manual"] = new Money(AmtTechnicalAciesCommission);
                    application["lux_techtotalcommexclegal_manual"] = new Money(AmtTechnicalTotalCommission);
                    application["lux_technicalpremiumbeforetaxexclegal"] = new Money(AmtTechnicalPremiumBT);
                    application["lux_letechnicalpremiumexcle"] = new Money(0);
                    application["lux_techtaxexclegal_manual"] = new Money(AmtTechnicalTax);
                    application["lux_totaltechpremiumexclegal_manual"] = new Money(AmtTechnicalPremiumTotal);

                    //Policy exc Legal
                    application["lux_policybrokercommexclegal_manual"] = new Money(AmtPolicyBrokerCommission);
                    application["lux_policyaciescommexclegal_manual"] = new Money(AmtPolicyAciesCommission);
                    application["lux_policytotalcommexclegal_manual"] = new Money(AmtPolicyTotalCommission);
                    application["lux_policypremiumbeforetaxexclegal"] = new Money(AmtPolicyPremiumBT);
                    application["lux_lepolicypremiumexcle"] = new Money(0);
                    application["lux_policytaxexclegal_manual"] = new Money(AmtPolicyTax);
                    application["lux_totalpolicypremiumexclegal_manual"] = new Money(AmtPolicyPremiumTotal);

                    //Technical inc Legal
                    application["lux_techbrokercommamount_manual"] = new Money(AmtTechnicalBrokerCommissionLe);
                    application["lux_techaciescommamount_manual"] = new Money(AmtTechnicalAciesCommissionLe);
                    application["lux_techtotalcommamount_manual"] = new Money(AmtTechnicalTotalCommissionLe);
                    application["lux_technicalpremiumbeforetax"] = new Money(AmtTechnicalPremiumBT);
                    application["lux_technicallegalpremiumbeforetaxinc"] = new Money(AmtTechnicalPremiumBTLe);
                    application["lux_techtaxamount_manual"] = new Money(AmtTechnicalTaxLe);
                    application["lux_totaltechpremiuminc_manual"] = new Money(AmtTechnicalPremiumTotalLe);

                    //Policy inc Legal
                    application["lux_policybrokercommamount_manual"] = new Money(AmtPolicyBrokerCommissionLe);
                    application["lux_policyaciescommamount_manual"] = new Money(AmtPolicyAciesCommissionLe);
                    application["lux_policytotalcommamount_manual"] = new Money(AmtPolicyTotalCommissionLe);
                    application["lux_policypremiumbeforetax"] = new Money(AmtPolicyPremiumBT);
                    application["lux_lepolicypremium"] = new Money(AmtPolicyPremiumBTLe);
                    application["lux_policytaxamount_manual"] = new Money(AmtPolicyTaxLe);
                    application["lux_totalpolicypremiuminc_manual"] = new Money(AmtPolicyPremiumTotalLe);

                    //Policy Fees
                    application["lux_technicalpolicyfee"] = new Money(Convert.ToDecimal(mavenBlueRatingResponseViewModel.RatingResponse.intermediateResults.AmtTechnicalPremiumFee));
                    application["lux_policypolicyfee"] = new Money(Convert.ToDecimal(mavenBlueRatingResponseViewModel.RatingResponse.intermediateResults.AmtPolicyPremiumFee));

                    service.Update(application);
                }
            }
            catch (Exception ex)
            {
                throw new Exception(ex.Message);
            }
        }

        private EntityCollection GetRecruitmentCategoryEntities(IOrganizationService service, Entity recruitmentQuote)
        {
            // Generate the FetchXml query
            string Catfetch = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                          <entity name='lux_recruitmentcategorys'>
                            <attribute name='lux_totalpayroll' />
                            <attribute name='lux_ofacceptingsdccontractualliability' />
                            <attribute name='createdon' />
                            <attribute name='lux_recruitmentcategorysid' />
                            <order attribute='lux_totalpayroll' descending='false' />
                            <filter type='and'>
                              <condition attribute='statecode' operator='eq' value='0' />
                              <condition attribute='lux_recruitmentquotes' operator='eq' uiname='' uitype='lux_recruitmentquotes' value='{recruitmentQuote.Id}' />
                            </filter>
                            <link-entity name='lux_recruitmenttrades' from='lux_recruitmenttradesid' to='lux_trade' visible='false' link-type='outer' alias='trade'>
                              <attribute name='lux_name' />
                            </link-entity>
                          </entity>
                        </fetch>";

            return service.RetrieveMultiple(new FetchExpression(Catfetch));
        }

        private EntityCollection GetBrokerCommissionEntities(IOrganizationService service, EntityReference broker, EntityReference product, DateTime quotationDate)
        {
            // Generate the FetchXml query for Broker Commission
            string BrokerFetch = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                              <entity name='lux_brokercommission'>
                                <attribute name='createdon' />
                                <attribute name='lux_product' />
                                <attribute name='lux_commission' />
                                <attribute name='lux_renewalcommission' />
                                <attribute name='lux_brokercommissionid' />
                                <order attribute='createdon' descending='false' />
                                <filter type='and'>
                                  <condition attribute='statecode' operator='eq' value='0' />
                                  <filter type='or'>
                                    <condition attribute='lux_effectivefrom' operator='on-or-before' value='{String.Format("{0:MM/dd/yyyy}", quotationDate)}' />
                                    <condition attribute='lux_effectivefrom' operator='null' />
                                  </filter>
                                  <filter type='or'>
                                    <condition attribute='lux_effectiveto' operator='on-or-after' value= '{String.Format("{0:MM/dd/yyyy}", quotationDate)}' />
                                    <condition attribute='lux_effectiveto' operator='null' />
                                  </filter>
                                  <condition attribute='lux_broker' operator='eq' uiname='' uitype='account' value='{broker.Id}' />
                                  <condition attribute='lux_product' operator='eq' uiname='' uitype='product' value='{product.Id}' />
                                </filter>
                              </entity>
                            </fetch>";

            return service.RetrieveMultiple(new FetchExpression(BrokerFetch));
        }

        private EntityCollection GetRecruitmentLocationEntities(IOrganizationService service, Entity recruitmentQuote)
        {
            string propfetch = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                          <entity name='lux_recruitmentlocations'>
                            <attribute name='lux_locationnumber' />
                            <attribute name='lux_istheriskaddressthesameasregisteredaddres' />
                            <attribute name='lux_location1postcode' />
                            <attribute name='lux_location1housenumber' />
                            <attribute name='lux_location1street' />
                            <attribute name='lux_location1towncity' />
                            <attribute name='lux_location1county' />
                            <attribute name='lux_buildingssuminsured' />
                            <attribute name='lux_geographicalarea' />
                            <attribute name='lux_tenantsimprovementssuminsured' />
                            <attribute name='lux_officecontentssuminsured' />
                            <attribute name='lux_computerselectronicequipmentsuminsured' />
                            <attribute name='lux_allriskssuminsuredoption' />
                            <attribute name='lux_pleaseselectterritoriallimitsrequiredfor' />
                            <attribute name='lux_recruitmentlocationsid' />
                            <order attribute='lux_locationnumber' descending='false' />
                            <filter type='and'>
                              <condition attribute='statecode' operator='eq' value='0' />
                              <condition attribute='lux_recruitmentquotes' operator='eq' uiname='' uitype='lux_recruitmentquotes' value='{recruitmentQuote.Id}' />
                            </filter>
                          </entity>
                        </fetch>";

            return service.RetrieveMultiple(new FetchExpression(propfetch));
        }

        private string GetAllRiskFetchXml(IOrganizationService service, Entity recruitmentQuote)
        {
            var propItems = GetRecruitmentLocationEntities(service, recruitmentQuote);

            string AllRiskItem = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                              <entity name='lux_risksuminsureddetails'>
                                <attribute name='lux_risksuminsureddetailsid' />
                                <attribute name='lux_name' />
                                <attribute name='lux_excess' />
                                <attribute name='lux_suminsured' />
                                <order attribute='lux_name' descending='false' />
                                <link-entity name='lux_recruitmentlocations' from='lux_recruitmentlocationsid' to='lux_recruitmentlocation' link-type='inner' alias='ac'>
                                  <attribute name='lux_geographicalarea' />
                                  <filter type='and'>
                                    <condition attribute='lux_allriskssuminsuredoption' operator='eq' value='1' />
                                    <condition attribute='lux_recruitmentquotes' operator='eq' uiname='' uitype='lux_recruitmentquotes' value='{recruitmentQuote.Id}' />
                                  </filter>
                                </link-entity>
                              </entity>
                            </fetch>";

            return AllRiskItem;
        }

        private void LogFailureToD365(IOrganizationService service, ITracingService tracing, string recordName, string apiMethod, string errorMessage)
        {
            tracing.Trace("In Loggig Failure");
            try
            {
                var logEntity = new Entity("lux_apilog");
                logEntity["lux_name"] = recordName;
                logEntity["lux_apimethod"] = apiMethod;
                logEntity["lux_errormessage"] = errorMessage;
                logEntity["createdon"] = DateTime.UtcNow;

                var id = service.Create(logEntity); // Synchronously create the log entry
                tracing.Trace($"Logged Error with id {id}");
            }
            catch (Exception logEx)
            {
                // In case logging fails, you can use tracing or another mechanism to log to the console or external system
                Console.WriteLine($"Error logging failure to D365: {logEx.Message}");
                tracing.Trace($"Logged Error Faield. {logEx.Message}");
            }
        }

        private async void LogFailureToD365UsingPowerAutomateFlow(IOrganizationService service, ITracingService tracing, string flowUrl, string recordName, string apiMethod, string errorMessage)
        {
            tracing.Trace("In Automate flow");
            var request = new RetrieveCurrentOrganizationRequest();
            var organzationResponse = (RetrieveCurrentOrganizationResponse)service.Execute(request);
            var uriString = organzationResponse.Detail.UrlName;

            try
            {
                using (var client = new HttpClient())
                {
                    string automateFlowurl = $"Live Url";

                    if (uriString.ToLower().Contains("uat"))
                    {
                        automateFlowurl = $"{flowUrl}";
                    }

                    var body = $"{{\"RecordName\": \"{recordName}\",\"ApiMethodName\":\"{apiMethod}\",\"ErrorMessage\":\"{errorMessage}\"}}";
                    var content = new StringContent(body, Encoding.UTF8, "application/json");

                    var response = await client.PostAsync(automateFlowurl, content);
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"{ex.InnerException?.Message ?? ex.Message}");
            }
        }

        public string GetEnvironmentVariableValue(IOrganizationService service, string schemaName)
        {
            var query = new QueryExpression("environmentvariablevalue")
            {
                ColumnSet = new ColumnSet("value"),
                LinkEntities =
                    {
                        new LinkEntity
                        {
                            LinkFromEntityName = "environmentvariablevalue",
                            LinkFromAttributeName = "environmentvariabledefinitionid",
                            LinkToEntityName = "environmentvariabledefinition",
                            LinkToAttributeName = "environmentvariabledefinitionid",
                            JoinOperator = JoinOperator.Inner,
                            LinkCriteria = new FilterExpression
                            {
                                Conditions =
                                {
                                    new ConditionExpression("schemaname", ConditionOperator.Equal, schemaName)
                                }
                            }
                        }
                    }
            };

            var results = service.RetrieveMultiple(query);

            if (results.Entities.Any())
            {
                var value = results.Entities.First().GetAttributeValue<string>("value");
                if (!string.IsNullOrEmpty(value))
                    return value;
            }

            throw new Exception($"Environment variable '{schemaName}' not found or has no value.");
        }

        public int ExtractLimitValue(string formattedValue)
        {
            if (string.IsNullOrWhiteSpace(formattedValue))
                return 0;

            var match = System.Text.RegularExpressions.Regex.Match(formattedValue, @"\d[\d,]*");

            if (match.Success)
            {
                var numberString = match.Value.Replace(",", "");
                if (int.TryParse(numberString, out int result))
                {
                    return result;
                }
            }

            return 0;
        }

        public Dictionary<int, string> GetSectionDiscountStrings(IOrganizationService service, Guid recruitmentQuoteId)
        {
            var fetchXml = $@"
                <fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                    <entity name='lux_specialistschemerecruitmentpremuim'>
                        <attribute name='lux_section' />
                        <attribute name='lux_loaddiscount' />
                        <filter type='and'>
                            <condition attribute='statecode' operator='eq' value='0' />
                            <condition attribute='lux_recruitmentquote' operator='eq' value='{recruitmentQuoteId}' />
                        </filter>
                    </entity>
                </fetch>";

            var discountResults = new Dictionary<int, string>();
            var rateLines = service.RetrieveMultiple(new FetchExpression(fetchXml)).Entities;

            foreach (var record in rateLines)
            {
                var sectionOption = record.GetAttributeValue<OptionSetValue>("lux_section");
                if (sectionOption == null) continue;

                int sectionValue = sectionOption.Value;
                decimal discount = record.Contains("lux_loaddiscount")
                    ? record.GetAttributeValue<decimal>("lux_loaddiscount")
                    : 0;

                // Invert and convert to fraction string
                decimal normalizedDiscount = (-discount) / 100;
                //string formattedDiscount = normalizedDiscount.ToString("0.##", CultureInfo.InvariantCulture);
                string formattedDiscount = normalizedDiscount.ToString();

                if (normalizedDiscount != 0 && !discountResults.ContainsKey(sectionValue))
                {
                    discountResults[sectionValue] = formattedDiscount;
                }
            }

            return discountResults;
        }

        private void UpsertPremiumSection(IOrganizationService service, Entity existingRecord, Entity recruitmentQuote, int sectionValue, string sectionName,
            decimal ratingFigure, decimal netPremium, decimal policyPremium, int rowOrder)
        {
            var premiumEntity = existingRecord != null
                ? service.Retrieve("lux_specialistschemerecruitmentpremuim", existingRecord.Id, new ColumnSet(true))
                : new Entity("lux_specialistschemerecruitmentpremuim");

            premiumEntity["lux_section"] = new OptionSetValue(sectionValue);
            premiumEntity["lux_name"] = sectionName;
            premiumEntity["lux_ratingfigures"] = new Money(ratingFigure);
            premiumEntity["lux_technicalpremium"] = new Money(netPremium);
            premiumEntity["lux_policypremium_manual"] = new Money(policyPremium);
            premiumEntity["lux_recruitmentquote"] = new EntityReference("lux_recruitmentquotes", recruitmentQuote.Id);
            premiumEntity["lux_roworder"] = rowOrder;

            if (existingRecord != null)
                service.Update(premiumEntity);
            else
                service.Create(premiumEntity);
        }

        public void CalculateAndSetCommissions(CodeActivityContext executionContext, IOrganizationService service, Entity recruitmentQuote, DateTime inceptionDate, decimal defaultTotalCommission,
            ref decimal defaultBrokerCommission, ref decimal defaultAciesComm)
        {
            var quotationDate = recruitmentQuote.Attributes.Contains("lux_quotationdate")
                ? recruitmentQuote.GetAttributeValue<DateTime>("lux_quotationdate")
                : inceptionDate;

            var brokerCommissions = GetBrokerCommissionEntities(
                service,
                Broker.Get(executionContext),
                Product.Get(executionContext),
                quotationDate
            );

            CalculateCommissions(
                recruitmentQuote,
                brokerCommissions,
                defaultTotalCommission,
                ref defaultBrokerCommission,
                ref defaultAciesComm
            );
        }

        public void CalculateCommissions(Entity recruitmentQuote, EntityCollection brokerCommissions, decimal defaultTotalCommission,
            ref decimal defaultBrokerCommission, ref decimal defaultAciesComm)
        {
            bool isTechBrokerComm = recruitmentQuote.Attributes.Contains("lux_technicalbrokercommission");

            if (!isTechBrokerComm)
            {
                if (brokerCommissions.Entities.Count > 0)
                {
                    var brokerCommission = brokerCommissions.Entities[0];

                    var applicationType = recruitmentQuote.GetAttributeValue<OptionSetValue>("lux_applicationtype")?.Value;

                    if (applicationType == 972970001) // NB
                    {
                        defaultBrokerCommission = brokerCommission.GetAttributeValue<decimal>("lux_commission");
                    }
                    else if (applicationType == 972970003) // Renewal
                    {
                        defaultBrokerCommission = brokerCommission.GetAttributeValue<decimal>("lux_renewalcommission");
                    }
                }
            }
            else
            {
                defaultBrokerCommission = recruitmentQuote.GetAttributeValue<decimal>("lux_technicalbrokercommission");
            }

            bool isAciesBrokerComm = recruitmentQuote.Attributes.Contains("lux_technicalaciescommission");

            if (!isAciesBrokerComm)
            {
                if (brokerCommissions.Entities.Count > 0)
                {
                    defaultAciesComm = defaultTotalCommission - defaultBrokerCommission;
                }
            }
            else
            {
                defaultAciesComm = recruitmentQuote.GetAttributeValue<decimal>("lux_technicalaciescommission");
            }
        }
    }
}