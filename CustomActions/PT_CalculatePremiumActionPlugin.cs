using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Acies_Customization.CustomActions
{
    public class PT_CalculatePremiumActionPlugin : IPlugin
    {
        public void Execute(IServiceProvider serviceProvider)
        {
            IPluginExecutionContext context = (IPluginExecutionContext)serviceProvider.GetService(typeof(IPluginExecutionContext));
            IOrganizationServiceFactory serviceFactory = (IOrganizationServiceFactory)serviceProvider.GetService(typeof(IOrganizationServiceFactory));
            IOrganizationService service = serviceFactory.CreateOrganizationService(context.UserId);
            ITracingService tracingService = (ITracingService)serviceProvider.GetService(typeof(ITracingService));
            tracingService.Trace("PT_CalculatePremiumActionPlugin execution started.");

            try
            {
                //foreach (var key in context.InputParameters.Keys)
                //{
                //    var val = context.InputParameters[key];
                //    tracingService.Trace($"Input key: {key}, value: {val}, type: {val?.GetType().FullName}");
                //}

                if (!(context.InputParameters.TryGetValue("Target", out var targetObj) && targetObj is EntityReference ptQuoteRef))
                {
                    throw new InvalidPluginExecutionException("PT record could not be identified.");
                }

                var columns = new ColumnSet(
                    "lux_pleaseconfirmtheliabilitylimitedrequired",
                    "lux_pleaseconfirmifcivilpatronalcoverisrequir",
                    "lux_pleaseconfirmthecivilpatronallimitedrequi",

                    "lux_iscoverrequiredforpropertydamage",
                    "lux_whatisthetotalvalueofallwharvesquays",

                    "lux_iscoverrequiredforcargohandlingequipment",
                    "lux_whatisthetotalvalueofallcargohandlingequi",

                    "lux_iscoverrequiredforbusinessinterruption",
                    "lux_pleaseconfirmthebusinessinterruptionlimit",

                    "lux_iscoverrequiredforportcraft",
                    "lux_whatisthetotalvalueofallportcraft",

                    "transactioncurrencyid",
                    "lux_name"
                );

                Guid ptQuoteId = ptQuoteRef.Id;
                Entity ptEntity = service.Retrieve(ptQuoteRef.LogicalName, ptQuoteId, columns);

                EntityReference premiumCurrency = ptEntity.GetAttributeValue<EntityReference>("transactioncurrencyid");
                string quoteNUmber = ptEntity.GetAttributeValue<string>("lux_name");

                bool patronalLiabilityCover = ptEntity.GetAttributeValue<bool?>("lux_pleaseconfirmifcivilpatronalcoverisrequir") ?? false;
                bool portPropertyCover = ptEntity.GetAttributeValue<bool?>("lux_iscoverrequiredforpropertydamage") ?? false;
                bool handlingEquipmentCover = ptEntity.GetAttributeValue<bool?>("lux_iscoverrequiredforcargohandlingequipment") ?? false;
                bool biCover = ptEntity.GetAttributeValue<bool?>("lux_iscoverrequiredforbusinessinterruption") ?? false;
                bool portCraftCover = ptEntity.GetAttributeValue<bool?>("lux_iscoverrequiredforportcraft") ?? false;

                const int SECTION_LIABILITY = 972970000;
                const int SECTION_PORT_PROPERTY = 972970001;
                const int SECTION_HANDLING_EQUIPMENT = 972970002;
                const int SECTION_BI = 972970003;
                const int SECTION_PORT_CRAFT = 972970004;

                EntityCollection rateItems = service.RetrieveMultiple(new QueryExpression("lux_portandterminalsquotepremium")
                {
                    ColumnSet = new ColumnSet("lux_section"),
                    Criteria = {
                        Conditions = {
                            new ConditionExpression("lux_portandterminalsquote", ConditionOperator.Equal, ptQuoteId)
                        }
                    }
                });

                var liabilityPremium = rateItems.Entities.FirstOrDefault(x => x.GetAttributeValue<OptionSetValue>("lux_section").Value == SECTION_LIABILITY);
                var portPropertyPremium = rateItems.Entities.FirstOrDefault(x => x.GetAttributeValue<OptionSetValue>("lux_section").Value == SECTION_PORT_PROPERTY);
                var handlingEquipmentPremium = rateItems.Entities.FirstOrDefault(x => x.GetAttributeValue<OptionSetValue>("lux_section").Value == SECTION_HANDLING_EQUIPMENT);
                var biPremium = rateItems.Entities.FirstOrDefault(x => x.GetAttributeValue<OptionSetValue>("lux_section").Value == SECTION_BI);
                var portCraftPremium = rateItems.Entities.FirstOrDefault(x => x.GetAttributeValue<OptionSetValue>("lux_section").Value == SECTION_PORT_CRAFT);

                // Liability
                decimal totalLimitLiability = ptEntity.GetAttributeValue<decimal>("lux_pleaseconfirmtheliabilitylimitedrequired");

                if (patronalLiabilityCover)
                {
                    decimal patronallimitLiabilityValue = ptEntity.GetAttributeValue<Money>("lux_pleaseconfirmthecivilpatronallimitedrequi").Value;
                    totalLimitLiability += patronallimitLiabilityValue;
                }

                string liabilityCoverageIdentifier = "GL";
                if (liabilityPremium == null)
                {
                    CreateRecord(service, "Liability", SECTION_LIABILITY, totalLimitLiability, 1, ptQuoteRef, premiumCurrency, quoteNUmber, liabilityCoverageIdentifier);
                }
                else
                {
                    UpdateRecord(service, totalLimitLiability, liabilityPremium.Id, premiumCurrency, quoteNUmber, liabilityCoverageIdentifier);
                }

                // Port Property
                if (portPropertyCover)
                {
                    string portCoverageIdentifier = "PD";
                    decimal portPropertyValue = ptEntity.GetAttributeValue<decimal>("lux_whatisthetotalvalueofallwharvesquays");
                    if (portPropertyPremium == null)
                    {
                        CreateRecord(service, "Port Property", SECTION_PORT_PROPERTY, portPropertyValue, 2, ptQuoteRef, premiumCurrency, quoteNUmber, portCoverageIdentifier);
                    }
                    else
                    {
                        UpdateRecord(service, portPropertyValue, portPropertyPremium.Id, premiumCurrency, quoteNUmber, portCoverageIdentifier);
                    }
                }
                else if (portPropertyPremium != null)
                {
                    DeleteRecord(service, portPropertyPremium.Id);
                }

                // Handling Equipment
                if (handlingEquipmentCover)
                {
                    string euipementCoverageIdentifier = "HE";
                    decimal handlingEquipmentValue = ptEntity.GetAttributeValue<decimal>("lux_whatisthetotalvalueofallcargohandlingequi");
                    if (handlingEquipmentPremium == null)
                    {
                        CreateRecord(service, "Temporary Buildings", SECTION_HANDLING_EQUIPMENT, handlingEquipmentValue, 3, ptQuoteRef, premiumCurrency, quoteNUmber, euipementCoverageIdentifier);
                    }
                    else
                    {
                        UpdateRecord(service, handlingEquipmentValue, handlingEquipmentPremium.Id, premiumCurrency, quoteNUmber, euipementCoverageIdentifier);
                    }
                }
                else if (handlingEquipmentPremium != null)
                {
                    DeleteRecord(service, handlingEquipmentPremium.Id);
                }

                // Business Intrupption
                if (biCover)
                {
                    string biCoverageIdentifier = "BI";
                    decimal biValue = ptEntity.GetAttributeValue<Money>("lux_pleaseconfirmthebusinessinterruptionlimit").Value;
                    if (biPremium == null)
                    {
                        CreateRecord(service, "Business Interruption", SECTION_BI, biValue, 4, ptQuoteRef, premiumCurrency, quoteNUmber, biCoverageIdentifier);
                    }
                    else
                    {
                        UpdateRecord(service, biValue, biPremium.Id, premiumCurrency, quoteNUmber, biCoverageIdentifier);
                    }
                }
                else if (biPremium != null)
                {
                    DeleteRecord(service, biPremium.Id);
                }

                // Port Craft
                if (portCraftCover)
                {
                    string portCraftCoverageIdentifier = "CF";
                    decimal portCraftValue = ptEntity.GetAttributeValue<decimal>("lux_whatisthetotalvalueofallportcraft");
                    if (portCraftPremium == null)
                    {
                        CreateRecord(service, "Port Craft", SECTION_PORT_CRAFT, portCraftValue, 5, ptQuoteRef, premiumCurrency, quoteNUmber, portCraftCoverageIdentifier);
                    }
                    else
                    {
                        UpdateRecord(service, portCraftValue, portCraftPremium.Id, premiumCurrency, quoteNUmber, portCraftCoverageIdentifier);
                    }
                }
                else if (portCraftPremium != null)
                {
                    DeleteRecord(service, portCraftPremium.Id);
                }
            }
            catch (Exception ex)
            {
                tracingService.Trace("Exception: {0}", ex.ToString());
                throw new InvalidPluginExecutionException($"An error occurred while calculating premium. {ex.InnerException?.Message ?? ex.Message}");
            }
        }

        private void CreateRecord(IOrganizationService service, string name, int section, decimal ratingFigure, int rowOrder, EntityReference ptQuoteReference, EntityReference premiumCurrency, string quoteNumber, string coverageIdentifier)
        {
            string sectionReference = GenerateSectionReference(quoteNumber, coverageIdentifier);

            Entity premiumEntity = new Entity("lux_portandterminalsquotepremium");
            premiumEntity["lux_name"] = name;
            premiumEntity["lux_section"] = new OptionSetValue(section);
            premiumEntity["lux_sectionreference"] = sectionReference;
            premiumEntity["lux_ratingfigures"] = new Money(ratingFigure);
            premiumEntity["lux_roworder"] = rowOrder;
            premiumEntity["lux_portandterminalsquote"] = ptQuoteReference;
            premiumEntity["transactioncurrencyid"] = premiumCurrency;
       
            service.Create(premiumEntity);
        }

        private void UpdateRecord(IOrganizationService service, decimal ratingFigure, Guid updateRecordId, EntityReference premiumCurrency, string quoteNumber, string coverageIdentifier)
        {
            string sectionReference = GenerateSectionReference(quoteNumber, coverageIdentifier);

            Entity premiumEntity = new Entity("lux_portandterminalsquotepremium", updateRecordId);
            premiumEntity["lux_sectionreference"] = sectionReference;
            premiumEntity["lux_ratingfigures"] = new Money(ratingFigure);
            premiumEntity["transactioncurrencyid"] = premiumCurrency;
            service.Update(premiumEntity);
        }

        private void DeleteRecord(IOrganizationService service, Guid deleteRecordId)
        {
            service.Delete("lux_portandterminalsquotepremium", deleteRecordId);
        }

        private string GenerateSectionReference(string quoteNumber, string coverageIdentifier)
        {
            string mguIdentifier = "TM";
            string clientIdentifier = quoteNumber.Substring(2, 5);
            string underwritingYear = quoteNumber.Substring(8, 2);

            return mguIdentifier + clientIdentifier + underwritingYear + coverageIdentifier;
        }
    }
}
