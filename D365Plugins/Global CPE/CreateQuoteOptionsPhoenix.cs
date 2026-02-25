using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using System;
using System.Linq;

namespace D365Plugins
{
    public class CreateQuoteOptionsPhoenix : IPlugin
    {
        public void Execute(IServiceProvider serviceProvider)
        {
            IPluginExecutionContext context = (IPluginExecutionContext)serviceProvider.GetService(typeof(IPluginExecutionContext));
            ITracingService tracingService = (ITracingService)serviceProvider.GetService(typeof(ITracingService));
            if (context.InputParameters.Contains("Target") && context.Depth == 1)
            {
                // Obtain the target entity from the input parameters.
                Entity entity = new Entity();
                try
                {
                    // Obtain the organization service reference.
                    IOrganizationServiceFactory serviceFactory = (IOrganizationServiceFactory)serviceProvider.GetService(typeof(IOrganizationServiceFactory));
                    IOrganizationService organizationService = serviceFactory.CreateOrganizationService(context.UserId);

                    if (context.MessageName != "Delete")
                    {
                        entity = (Entity)context.InputParameters["Target"];
                    }
                    else
                    {
                        EntityReference e = (EntityReference)context.InputParameters["Target"];
                        entity = organizationService.Retrieve(e.LogicalName, e.Id, new ColumnSet(true));
                    }

                    var cpeQuote = new Entity();
                    var quoteoption = "No";
                    var Product = "";

                    if (entity.LogicalName == "lux_contractorsplantandequipmentquote")
                    {
                        cpeQuote = organizationService.Retrieve(entity.LogicalName, entity.Id, new ColumnSet(true));
                        quoteoption = cpeQuote.Attributes.Contains("lux_wouldyouliketooffermultiplequoteoptions") ? cpeQuote.FormattedValues["lux_wouldyouliketooffermultiplequoteoptions"] : "No";
                        Product = cpeQuote.FormattedValues["lux_product"];

                        var OptionCount = cpeQuote.Contains("lux_quoteoptionscount") ? cpeQuote.GetAttributeValue<int>("lux_quoteoptionscount") : 0;
                        var targetpremium = cpeQuote.Attributes.Contains("lux_targetpremiumexcludingipt") ? cpeQuote.GetAttributeValue<Money>("lux_targetpremiumexcludingipt").Value : 0;

                        var QuoteOptionId = new Guid();

                        if (cpeQuote.Attributes.Contains("lux_quoteoption1"))
                        {
                            QuoteOptionId = cpeQuote.GetAttributeValue<EntityReference>("lux_quoteoption1").Id;

                            Entity cpequoteoption = organizationService.Retrieve("lux_phoenixquoteoption", QuoteOptionId, new ColumnSet(false));
                            cpequoteoption["transactioncurrencyid"] = new EntityReference("transactioncurrency", cpeQuote.GetAttributeValue<EntityReference>("transactioncurrencyid").Id);
                            organizationService.Update(cpequoteoption);

                            if (!cpeQuote.Contains("lux_quoteoptionselected"))
                            {
                                Entity ptAppln = organizationService.Retrieve("lux_contractorsplantandequipmentquote", cpeQuote.Id, new ColumnSet(false));
                                ptAppln["lux_quoteoptionselected"] = new EntityReference("lux_phoenixquoteoption", QuoteOptionId);
                                organizationService.Update(ptAppln);
                            }
                        }
                        else
                        {
                            Entity cpequoteoption = new Entity("lux_phoenixquoteoption");
                            cpequoteoption["lux_name"] = "Quote Option 1";
                            cpequoteoption["transactioncurrencyid"] = new EntityReference("transactioncurrency", cpeQuote.GetAttributeValue<EntityReference>("transactioncurrencyid").Id);
                            cpequoteoption["lux_contractorsplantandequipmentquote"] = new EntityReference("lux_contractorsplantandequipmentquote", cpeQuote.Id);

                            QuoteOptionId = organizationService.Create(cpequoteoption);

                            Entity ptAppln = organizationService.Retrieve("lux_contractorsplantandequipmentquote", cpeQuote.Id, new ColumnSet(false));
                            ptAppln["lux_quoteoption1"] = new EntityReference("lux_phoenixquoteoption", QuoteOptionId);
                            ptAppln["lux_quoteoptionselected"] = new EntityReference("lux_phoenixquoteoption", QuoteOptionId);
                            ptAppln["lux_quoteoptionscount"] = 1;
                            organizationService.Update(ptAppln);
                        }

                        var Ratingfetch = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                                                      <entity name='lux_phoenixquoteoptionlist'>
                                                        <attribute name='lux_name' />
                                                        <attribute name='lux_rownumber' />
                                                        <attribute name='transactioncurrencyid' />
                                                        <attribute name='lux_phoenixquoteoptionlistid' />
                                                        <order attribute='lux_name' descending='false' />
                                                        <filter type='and'>
                                                          <condition attribute='statecode' operator='eq' value='0' />
                                                          <condition attribute='lux_phoenixquoteoption' operator='eq' uiname='' uitype='lux_phoenixquoteoption' value='{QuoteOptionId}' />
                                                        </filter>
                                                      </entity>
                                                    </fetch>";

                        var RateItem = organizationService.RetrieveMultiple(new FetchExpression(Ratingfetch)).Entities;
                        if (Product == "Contractors Plant and Equipment")
                        {
                            var OwnPlant = RateItem.FirstOrDefault(x => x.GetAttributeValue<int>("lux_rownumber") == 1);
                            var HiredinPlant = RateItem.FirstOrDefault(x => x.GetAttributeValue<int>("lux_rownumber") == 2);
                            var TemporaryBuildings = RateItem.FirstOrDefault(x => x.GetAttributeValue<int>("lux_rownumber") == 3);
                            var Employeestools = RateItem.FirstOrDefault(x => x.GetAttributeValue<int>("lux_rownumber") == 4);
                            var Otheritems = RateItem.FirstOrDefault(x => x.GetAttributeValue<int>("lux_rownumber") == 5);
                            var Increasedcostofworking = RateItem.FirstOrDefault(x => x.GetAttributeValue<int>("lux_rownumber") == 6);
                            var Terrorism = RateItem.FirstOrDefault(x => x.GetAttributeValue<int>("lux_rownumber") == 7);

                            var MPL = 0M;
                            //throw new InvalidPluginExecutionException(cpeQuote.GetAttributeValue<bool>("lux_pleaseconfirmifcoverforownplantisrequired").ToString());
                            if (cpeQuote.Contains("lux_pleaseconfirmifcoverforownplantisrequired") && cpeQuote.GetAttributeValue<bool>("lux_pleaseconfirmifcoverforownplantisrequired") == true)
                            {
                                var Limit = cpeQuote.GetAttributeValue<decimal>("lux_totalownplantvalue");
                                MPL += cpeQuote.Contains("lux_ownplantanyoneoccurrence") ? cpeQuote.GetAttributeValue<decimal>("lux_ownplantanyoneoccurrence") : 0;

                                Entity ownPlant = new Entity("lux_phoenixquoteoptionlist");
                                if (OwnPlant != null)
                                {
                                    ownPlant = organizationService.Retrieve("lux_phoenixquoteoptionlist", OwnPlant.Id, new ColumnSet(true));
                                }
                                ownPlant["lux_cover"] = new OptionSetValue(972970000);
                                ownPlant["lux_limitofindemnity"] = Limit;
                                ownPlant["lux_limitofindemnityformatted"] = Limit.ToString("#,##0.00");
                                ownPlant["transactioncurrencyid"] = new EntityReference("transactioncurrency", cpeQuote.GetAttributeValue<EntityReference>("transactioncurrencyid").Id);
                                ownPlant["lux_riskcurrency"] = new EntityReference("transactioncurrency", cpeQuote.GetAttributeValue<EntityReference>("lux_riskcurrency").Id);
                                ownPlant["lux_phoenixquoteoption"] = new EntityReference("lux_phoenixquoteoption", QuoteOptionId);

                                if (OwnPlant != null)
                                {
                                    organizationService.Update(ownPlant);
                                }
                                else
                                {
                                    organizationService.Create(ownPlant);
                                }
                            }
                            else
                            {
                                foreach (var item in RateItem.Where(x => x.GetAttributeValue<int>("lux_rownumber") == 1))
                                {
                                    organizationService.Delete("lux_phoenixquoteoptionlist", item.Id);
                                }
                            }

                            if (cpeQuote.Contains("lux_pleaseconfirmifcoverforhiredinplantisrequ") && cpeQuote.GetAttributeValue<bool>("lux_pleaseconfirmifcoverforhiredinplantisrequ") == true)
                            {
                                var Limit = cpeQuote.Contains("lux_totalhiredplantvalue") ? cpeQuote.GetAttributeValue<decimal>("lux_totalhiredplantvalue") : 0M;

                                Entity hiredinPlant = new Entity("lux_phoenixquoteoptionlist");
                                if (HiredinPlant != null)
                                {
                                    hiredinPlant = organizationService.Retrieve("lux_phoenixquoteoptionlist", HiredinPlant.Id, new ColumnSet(true));
                                }
                                hiredinPlant["lux_cover"] = new OptionSetValue(972970001);
                                hiredinPlant["lux_limitofindemnity"] = Limit;
                                hiredinPlant["lux_limitofindemnityformatted"] = Limit.ToString("#,##0.00");
                                hiredinPlant["transactioncurrencyid"] = new EntityReference("transactioncurrency", cpeQuote.GetAttributeValue<EntityReference>("transactioncurrencyid").Id);
                                hiredinPlant["lux_riskcurrency"] = new EntityReference("transactioncurrency", cpeQuote.GetAttributeValue<EntityReference>("lux_riskcurrency").Id);
                                hiredinPlant["lux_phoenixquoteoption"] = new EntityReference("lux_phoenixquoteoption", QuoteOptionId);

                                if (HiredinPlant != null)
                                {
                                    organizationService.Update(hiredinPlant);
                                }
                                else
                                {
                                    organizationService.Create(hiredinPlant);
                                }
                            }
                            else
                            {
                                foreach (var item in RateItem.Where(x => x.GetAttributeValue<int>("lux_rownumber") == 2))
                                {
                                    organizationService.Delete("lux_phoenixquoteoptionlist", item.Id);
                                }
                            }

                            if (cpeQuote.Contains("lux_pleaseconfirmifcoverfortemporarybuildings") && cpeQuote.GetAttributeValue<bool>("lux_pleaseconfirmifcoverfortemporarybuildings") == true)
                            {
                                var Limit = cpeQuote.Contains("lux_temporarybuildingssuminsured") ? cpeQuote.GetAttributeValue<decimal>("lux_temporarybuildingssuminsured") : 0M;
                                MPL += cpeQuote.Contains("lux_temporarybuildingsanyoneoccurrence") ? cpeQuote.GetAttributeValue<decimal>("lux_temporarybuildingsanyoneoccurrence") : 0;

                                Entity temporaryBuildings = new Entity("lux_phoenixquoteoptionlist");
                                if (TemporaryBuildings != null)
                                {
                                    temporaryBuildings = organizationService.Retrieve("lux_phoenixquoteoptionlist", TemporaryBuildings.Id, new ColumnSet(true));
                                }
                                temporaryBuildings["lux_cover"] = new OptionSetValue(972970002);
                                temporaryBuildings["lux_limitofindemnity"] = Limit;
                                temporaryBuildings["lux_limitofindemnityformatted"] = Limit.ToString("#,##0.00");
                                temporaryBuildings["transactioncurrencyid"] = new EntityReference("transactioncurrency", cpeQuote.GetAttributeValue<EntityReference>("transactioncurrencyid").Id);
                                temporaryBuildings["lux_riskcurrency"] = new EntityReference("transactioncurrency", cpeQuote.GetAttributeValue<EntityReference>("lux_riskcurrency").Id);
                                temporaryBuildings["lux_phoenixquoteoption"] = new EntityReference("lux_phoenixquoteoption", QuoteOptionId);
                                if (TemporaryBuildings != null)
                                {
                                    organizationService.Update(temporaryBuildings);
                                }
                                else
                                {
                                    organizationService.Create(temporaryBuildings);
                                }
                            }
                            else
                            {
                                foreach (var item in RateItem.Where(x => x.GetAttributeValue<int>("lux_rownumber") == 3))
                                {
                                    organizationService.Delete("lux_phoenixquoteoptionlist", item.Id);
                                }
                            }

                            if (cpeQuote.Contains("lux_pleaseconfirmifcoverforemployeestoolsisre") && cpeQuote.GetAttributeValue<bool>("lux_pleaseconfirmifcoverforemployeestoolsisre") == true)
                            {
                                var Limit = cpeQuote.Contains("lux_employeestoolstotalvalue") ? cpeQuote.GetAttributeValue<decimal>("lux_employeestoolstotalvalue") : 0M;
                                var Excess = cpeQuote.Contains("lux_employeetoolsexcess") ? cpeQuote.GetAttributeValue<decimal>("lux_employeetoolsexcess") : 0M;
                                MPL += cpeQuote.Contains("lux_employeestoolsanyoneoccurrence") ? cpeQuote.GetAttributeValue<decimal>("lux_employeestoolsanyoneoccurrence") : 0;

                                Entity employeestools = new Entity("lux_phoenixquoteoptionlist");
                                if (Employeestools != null)
                                {
                                    employeestools = organizationService.Retrieve("lux_phoenixquoteoptionlist", Employeestools.Id, new ColumnSet(true));
                                }
                                employeestools["lux_cover"] = new OptionSetValue(972970003);
                                employeestools["lux_limitofindemnity"] = Limit;
                                employeestools["lux_excess"] = Excess;
                                employeestools["lux_limitofindemnityformatted"] = Limit.ToString("#,##0.00");
                                employeestools["lux_excessformatted"] = Excess.ToString("#,##0.00");
                                employeestools["transactioncurrencyid"] = new EntityReference("transactioncurrency", cpeQuote.GetAttributeValue<EntityReference>("transactioncurrencyid").Id);
                                employeestools["lux_riskcurrency"] = new EntityReference("transactioncurrency", cpeQuote.GetAttributeValue<EntityReference>("lux_riskcurrency").Id);
                                employeestools["lux_phoenixquoteoption"] = new EntityReference("lux_phoenixquoteoption", QuoteOptionId);
                                if (Employeestools != null)
                                {
                                    organizationService.Update(employeestools);
                                }
                                else
                                {
                                    organizationService.Create(employeestools);
                                }
                            }
                            else
                            {
                                foreach (var item in RateItem.Where(x => x.GetAttributeValue<int>("lux_rownumber") == 4))
                                {
                                    organizationService.Delete("lux_phoenixquoteoptionlist", item.Id);
                                }
                            }

                            if (cpeQuote.Contains("lux_pleaseconfirmifcoverforotheritemsisrequir") && cpeQuote.GetAttributeValue<bool>("lux_pleaseconfirmifcoverforotheritemsisrequir") == true)
                            {
                                var Limit = cpeQuote.Contains("lux_otheritemslimit") ? cpeQuote.GetAttributeValue<decimal>("lux_otheritemslimit") : 0M;
                                var Excess = cpeQuote.Contains("lux_otheritemsexcess") ? cpeQuote.GetAttributeValue<decimal>("lux_otheritemsexcess") : 0M;
                                MPL += cpeQuote.Contains("lux_otheritemsanyoneoccurrence") ? cpeQuote.GetAttributeValue<decimal>("lux_otheritemsanyoneoccurrence") : 0;

                                Entity otheritems = new Entity("lux_phoenixquoteoptionlist");
                                if (Otheritems != null)
                                {
                                    otheritems = organizationService.Retrieve("lux_phoenixquoteoptionlist", Otheritems.Id, new ColumnSet(true));
                                }
                                otheritems["lux_cover"] = new OptionSetValue(972970004);
                                otheritems["lux_limitofindemnity"] = Limit;
                                otheritems["lux_excess"] = Excess;
                                otheritems["lux_limitofindemnityformatted"] = Limit.ToString("#,##0.00");
                                otheritems["lux_excessformatted"] = Excess.ToString("#,##0.00");
                                otheritems["transactioncurrencyid"] = new EntityReference("transactioncurrency", cpeQuote.GetAttributeValue<EntityReference>("transactioncurrencyid").Id);
                                otheritems["lux_riskcurrency"] = new EntityReference("transactioncurrency", cpeQuote.GetAttributeValue<EntityReference>("lux_riskcurrency").Id);
                                otheritems["lux_phoenixquoteoption"] = new EntityReference("lux_phoenixquoteoption", QuoteOptionId);
                                if (Otheritems != null)
                                {
                                    organizationService.Update(otheritems);
                                }
                                else
                                {
                                    organizationService.Create(otheritems);
                                }
                            }
                            else
                            {
                                foreach (var item in RateItem.Where(x => x.GetAttributeValue<int>("lux_rownumber") == 5))
                                {
                                    organizationService.Delete("lux_phoenixquoteoptionlist", item.Id);
                                }
                            }

                            if (cpeQuote.Contains("lux_pleaseconfirmifcoverforincreasedcostofwor") && cpeQuote.GetAttributeValue<bool>("lux_pleaseconfirmifcoverforincreasedcostofwor") == true)
                            {
                                var Limit = cpeQuote.Contains("lux_increasedcostofworkinglimitofindemnity") ? cpeQuote.GetAttributeValue<decimal>("lux_increasedcostofworkinglimitofindemnity") : 0M;

                                Entity increasedcostofworking = new Entity("lux_phoenixquoteoptionlist");
                                if (Increasedcostofworking != null)
                                {
                                    increasedcostofworking = organizationService.Retrieve("lux_phoenixquoteoptionlist", Increasedcostofworking.Id, new ColumnSet(true));
                                }
                                increasedcostofworking["lux_cover"] = new OptionSetValue(972970005);
                                increasedcostofworking["lux_limitofindemnity"] = Limit;
                                increasedcostofworking["lux_limitofindemnityformatted"] = Limit.ToString("#,##0.00");
                                increasedcostofworking["transactioncurrencyid"] = new EntityReference("transactioncurrency", cpeQuote.GetAttributeValue<EntityReference>("transactioncurrencyid").Id);
                                increasedcostofworking["lux_riskcurrency"] = new EntityReference("transactioncurrency", cpeQuote.GetAttributeValue<EntityReference>("lux_riskcurrency").Id);
                                increasedcostofworking["lux_phoenixquoteoption"] = new EntityReference("lux_phoenixquoteoption", QuoteOptionId);
                                if (Increasedcostofworking != null)
                                {
                                    organizationService.Update(increasedcostofworking);
                                }
                                else
                                {
                                    organizationService.Create(increasedcostofworking);
                                }
                            }
                            else
                            {
                                foreach (var item in RateItem.Where(x => x.GetAttributeValue<int>("lux_rownumber") == 6))
                                {
                                    organizationService.Delete("lux_phoenixquoteoptionlist", item.Id);
                                }
                            }

                            if (cpeQuote.Contains("lux_iscoverrequiredforterrorism") && cpeQuote.GetAttributeValue<bool>("lux_iscoverrequiredforterrorism") == true)
                            {
                                Entity terrorism = new Entity("lux_phoenixquoteoptionlist");
                                if (Terrorism != null)
                                {
                                    terrorism = organizationService.Retrieve("lux_phoenixquoteoptionlist", Terrorism.Id, new ColumnSet(true));
                                }
                                terrorism["lux_cover"] = new OptionSetValue(972970006);
                                terrorism["transactioncurrencyid"] = new EntityReference("transactioncurrency", cpeQuote.GetAttributeValue<EntityReference>("transactioncurrencyid").Id);
                                terrorism["lux_riskcurrency"] = new EntityReference("transactioncurrency", cpeQuote.GetAttributeValue<EntityReference>("lux_riskcurrency").Id);
                                terrorism["lux_phoenixquoteoption"] = new EntityReference("lux_phoenixquoteoption", QuoteOptionId);
                                if (Terrorism != null)
                                {
                                    organizationService.Update(terrorism);
                                }
                                else
                                {
                                    organizationService.Create(terrorism);
                                }
                            }
                            else
                            {
                                foreach (var item in RateItem.Where(x => x.GetAttributeValue<int>("lux_rownumber") == 7))
                                {
                                    organizationService.Delete("lux_phoenixquoteoptionlist", item.Id);
                                }
                            }

                            if (cpeQuote.GetAttributeValue<OptionSetValue>("lux_carrier").Value != 972970001)
                            {
                                Entity cpeAppln = organizationService.Retrieve("lux_contractorsplantandequipmentquote", cpeQuote.Id, new ColumnSet(false));
                                cpeAppln["lux_totalmpl100"] = MPL;
                                organizationService.Update(cpeAppln);
                            }
                        }
                        else if (Product == "Contractors All Risk")
                        {
                            var OwnPlant = RateItem.FirstOrDefault(x => x.GetAttributeValue<int>("lux_rownumber") == 1);
                            var HiredinPlant = RateItem.FirstOrDefault(x => x.GetAttributeValue<int>("lux_rownumber") == 2);
                            var TemporaryBuildings = RateItem.FirstOrDefault(x => x.GetAttributeValue<int>("lux_rownumber") == 3);
                            var Employeestools = RateItem.FirstOrDefault(x => x.GetAttributeValue<int>("lux_rownumber") == 4);
                            var Otheritems = RateItem.FirstOrDefault(x => x.GetAttributeValue<int>("lux_rownumber") == 5);
                            var Increasedcostofworking = RateItem.FirstOrDefault(x => x.GetAttributeValue<int>("lux_rownumber") == 6);

                            var Terrorism = RateItem.FirstOrDefault(x => x.GetAttributeValue<int>("lux_rownumber") == 7);

                            var OffSiteStorage = RateItem.FirstOrDefault(x => x.GetAttributeValue<int>("lux_rownumber") == 8);
                            var MaximumContractPrice = RateItem.FirstOrDefault(x => x.GetAttributeValue<int>("lux_rownumber") == 9);
                            var ExistingStructures = RateItem.FirstOrDefault(x => x.GetAttributeValue<int>("lux_rownumber") == 10);
                            var ShowPropertiesContents = RateItem.FirstOrDefault(x => x.GetAttributeValue<int>("lux_rownumber") == 11);
                            var ThirdPartyLiability = RateItem.FirstOrDefault(x => x.GetAttributeValue<int>("lux_rownumber") == 12);
                            var Delayinstartup = RateItem.FirstOrDefault(x => x.GetAttributeValue<int>("lux_rownumber") == 13);

                            var MPL = 0M;

                            if (cpeQuote.Contains("lux_pleaseconfirmifcoverforownplantisrequired") && cpeQuote.GetAttributeValue<bool>("lux_pleaseconfirmifcoverforownplantisrequired") == true)
                            {
                                var Limit = cpeQuote.GetAttributeValue<decimal>("lux_totalownplantvalue");
                                MPL += cpeQuote.Contains("lux_ownplantanyoneoccurrence") ? cpeQuote.GetAttributeValue<decimal>("lux_ownplantanyoneoccurrence") : 0;

                                Entity ownPlant = new Entity("lux_phoenixquoteoptionlist");
                                if (OwnPlant != null)
                                {
                                    ownPlant = organizationService.Retrieve("lux_phoenixquoteoptionlist", OwnPlant.Id, new ColumnSet(true));
                                }
                                ownPlant["lux_cover"] = new OptionSetValue(972970000);
                                ownPlant["lux_limitofindemnity"] = Limit;
                                ownPlant["lux_limitofindemnityformatted"] = Limit.ToString("#,##0.00");
                                ownPlant["transactioncurrencyid"] = new EntityReference("transactioncurrency", cpeQuote.GetAttributeValue<EntityReference>("transactioncurrencyid").Id);
                                ownPlant["lux_riskcurrency"] = new EntityReference("transactioncurrency", cpeQuote.GetAttributeValue<EntityReference>("lux_riskcurrency").Id);
                                ownPlant["lux_phoenixquoteoption"] = new EntityReference("lux_phoenixquoteoption", QuoteOptionId);

                                if (OwnPlant != null)
                                {
                                    organizationService.Update(ownPlant);
                                }
                                else
                                {
                                    organizationService.Create(ownPlant);
                                }
                            }
                            else
                            {
                                foreach (var item in RateItem.Where(x => x.GetAttributeValue<int>("lux_rownumber") == 1))
                                {
                                    organizationService.Delete("lux_phoenixquoteoptionlist", item.Id);
                                }
                            }

                            if (cpeQuote.Contains("lux_pleaseconfirmifcoverforhiredinplantisrequ") && cpeQuote.GetAttributeValue<bool>("lux_pleaseconfirmifcoverforhiredinplantisrequ") == true)
                            {
                                var Limit = cpeQuote.Contains("lux_totalhiredplantvalue") ? cpeQuote.GetAttributeValue<decimal>("lux_totalhiredplantvalue") : 0M;

                                Entity hiredinPlant = new Entity("lux_phoenixquoteoptionlist");
                                if (HiredinPlant != null)
                                {
                                    hiredinPlant = organizationService.Retrieve("lux_phoenixquoteoptionlist", HiredinPlant.Id, new ColumnSet(true));
                                }
                                hiredinPlant["lux_cover"] = new OptionSetValue(972970001);
                                hiredinPlant["lux_limitofindemnity"] = Limit;
                                hiredinPlant["lux_limitofindemnityformatted"] = Limit.ToString("#,##0.00");
                                hiredinPlant["transactioncurrencyid"] = new EntityReference("transactioncurrency", cpeQuote.GetAttributeValue<EntityReference>("transactioncurrencyid").Id);
                                hiredinPlant["lux_riskcurrency"] = new EntityReference("transactioncurrency", cpeQuote.GetAttributeValue<EntityReference>("lux_riskcurrency").Id);
                                hiredinPlant["lux_phoenixquoteoption"] = new EntityReference("lux_phoenixquoteoption", QuoteOptionId);

                                if (HiredinPlant != null)
                                {
                                    organizationService.Update(hiredinPlant);
                                }
                                else
                                {
                                    organizationService.Create(hiredinPlant);
                                }
                            }
                            else
                            {
                                foreach (var item in RateItem.Where(x => x.GetAttributeValue<int>("lux_rownumber") == 2))
                                {
                                    organizationService.Delete("lux_phoenixquoteoptionlist", item.Id);
                                }
                            }

                            if (cpeQuote.Contains("lux_pleaseconfirmifcoverfortemporarybuildings") && cpeQuote.GetAttributeValue<bool>("lux_pleaseconfirmifcoverfortemporarybuildings") == true)
                            {
                                var Limit = cpeQuote.Contains("lux_temporarybuildingssuminsured") ? cpeQuote.GetAttributeValue<decimal>("lux_temporarybuildingssuminsured") : 0M;
                                MPL += cpeQuote.Contains("lux_temporarybuildingsanyoneoccurrence") ? cpeQuote.GetAttributeValue<decimal>("lux_temporarybuildingsanyoneoccurrence") : 0;

                                Entity temporaryBuildings = new Entity("lux_phoenixquoteoptionlist");
                                if (TemporaryBuildings != null)
                                {
                                    temporaryBuildings = organizationService.Retrieve("lux_phoenixquoteoptionlist", TemporaryBuildings.Id, new ColumnSet(true));
                                }
                                temporaryBuildings["lux_cover"] = new OptionSetValue(972970002);
                                temporaryBuildings["lux_limitofindemnity"] = Limit;
                                temporaryBuildings["lux_limitofindemnityformatted"] = Limit.ToString("#,##0.00");
                                temporaryBuildings["transactioncurrencyid"] = new EntityReference("transactioncurrency", cpeQuote.GetAttributeValue<EntityReference>("transactioncurrencyid").Id);
                                temporaryBuildings["lux_riskcurrency"] = new EntityReference("transactioncurrency", cpeQuote.GetAttributeValue<EntityReference>("lux_riskcurrency").Id);
                                temporaryBuildings["lux_phoenixquoteoption"] = new EntityReference("lux_phoenixquoteoption", QuoteOptionId);
                                if (TemporaryBuildings != null)
                                {
                                    organizationService.Update(temporaryBuildings);
                                }
                                else
                                {
                                    organizationService.Create(temporaryBuildings);
                                }
                            }
                            else
                            {
                                foreach (var item in RateItem.Where(x => x.GetAttributeValue<int>("lux_rownumber") == 3))
                                {
                                    organizationService.Delete("lux_phoenixquoteoptionlist", item.Id);
                                }
                            }

                            if (cpeQuote.Contains("lux_pleaseconfirmifcoverforemployeestoolsisre") && cpeQuote.GetAttributeValue<bool>("lux_pleaseconfirmifcoverforemployeestoolsisre") == true)
                            {
                                var Limit = cpeQuote.Contains("lux_employeestoolstotalvalue") ? cpeQuote.GetAttributeValue<decimal>("lux_employeestoolstotalvalue") : 0M;
                                var Excess = cpeQuote.Contains("lux_employeetoolsexcess") ? cpeQuote.GetAttributeValue<decimal>("lux_employeetoolsexcess") : 0M;
                                MPL += cpeQuote.Contains("lux_employeestoolsanyoneoccurrence") ? cpeQuote.GetAttributeValue<decimal>("lux_employeestoolsanyoneoccurrence") : 0;

                                Entity employeestools = new Entity("lux_phoenixquoteoptionlist");
                                if (Employeestools != null)
                                {
                                    employeestools = organizationService.Retrieve("lux_phoenixquoteoptionlist", Employeestools.Id, new ColumnSet(true));
                                }
                                employeestools["lux_cover"] = new OptionSetValue(972970003);
                                employeestools["lux_limitofindemnity"] = Limit;
                                employeestools["lux_excess"] = Excess;
                                employeestools["lux_limitofindemnityformatted"] = Limit.ToString("#,##0.00");
                                employeestools["lux_excessformatted"] = Excess.ToString("#,##0.00");
                                employeestools["transactioncurrencyid"] = new EntityReference("transactioncurrency", cpeQuote.GetAttributeValue<EntityReference>("transactioncurrencyid").Id);
                                employeestools["lux_riskcurrency"] = new EntityReference("transactioncurrency", cpeQuote.GetAttributeValue<EntityReference>("lux_riskcurrency").Id);
                                employeestools["lux_phoenixquoteoption"] = new EntityReference("lux_phoenixquoteoption", QuoteOptionId);
                                if (Employeestools != null)
                                {
                                    organizationService.Update(employeestools);
                                }
                                else
                                {
                                    organizationService.Create(employeestools);
                                }
                            }
                            else
                            {
                                foreach (var item in RateItem.Where(x => x.GetAttributeValue<int>("lux_rownumber") == 4))
                                {
                                    organizationService.Delete("lux_phoenixquoteoptionlist", item.Id);
                                }
                            }

                            if (cpeQuote.Contains("lux_pleaseconfirmifcoverforotheritemsisrequir") && cpeQuote.GetAttributeValue<bool>("lux_pleaseconfirmifcoverforotheritemsisrequir") == true)
                            {
                                var Limit = cpeQuote.Contains("lux_otheritemslimit") ? cpeQuote.GetAttributeValue<decimal>("lux_otheritemslimit") : 0M;
                                var Excess = cpeQuote.Contains("lux_otheritemsexcess") ? cpeQuote.GetAttributeValue<decimal>("lux_otheritemsexcess") : 0M;
                                MPL += cpeQuote.Contains("lux_otheritemsanyoneoccurrence") ? cpeQuote.GetAttributeValue<decimal>("lux_otheritemsanyoneoccurrence") : 0;

                                Entity otheritems = new Entity("lux_phoenixquoteoptionlist");
                                if (Otheritems != null)
                                {
                                    otheritems = organizationService.Retrieve("lux_phoenixquoteoptionlist", Otheritems.Id, new ColumnSet(true));
                                }
                                otheritems["lux_cover"] = new OptionSetValue(972970004);
                                otheritems["lux_limitofindemnity"] = Limit;
                                otheritems["lux_excess"] = Excess;
                                otheritems["lux_limitofindemnityformatted"] = Limit.ToString("#,##0.00");
                                otheritems["lux_excessformatted"] = Excess.ToString("#,##0.00");
                                otheritems["transactioncurrencyid"] = new EntityReference("transactioncurrency", cpeQuote.GetAttributeValue<EntityReference>("transactioncurrencyid").Id);
                                otheritems["lux_riskcurrency"] = new EntityReference("transactioncurrency", cpeQuote.GetAttributeValue<EntityReference>("lux_riskcurrency").Id);
                                otheritems["lux_phoenixquoteoption"] = new EntityReference("lux_phoenixquoteoption", QuoteOptionId);
                                if (Otheritems != null)
                                {
                                    organizationService.Update(otheritems);
                                }
                                else
                                {
                                    organizationService.Create(otheritems);
                                }
                            }
                            else
                            {
                                foreach (var item in RateItem.Where(x => x.GetAttributeValue<int>("lux_rownumber") == 5))
                                {
                                    organizationService.Delete("lux_phoenixquoteoptionlist", item.Id);
                                }
                            }

                            if (cpeQuote.Contains("lux_pleaseconfirmifcoverforincreasedcostofwor") && cpeQuote.GetAttributeValue<bool>("lux_pleaseconfirmifcoverforincreasedcostofwor") == true)
                            {
                                var Limit = cpeQuote.Contains("lux_increasedcostofworkinglimitofindemnity") ? cpeQuote.GetAttributeValue<decimal>("lux_increasedcostofworkinglimitofindemnity") : 0M;

                                Entity increasedcostofworking = new Entity("lux_phoenixquoteoptionlist");
                                if (Increasedcostofworking != null)
                                {
                                    increasedcostofworking = organizationService.Retrieve("lux_phoenixquoteoptionlist", Increasedcostofworking.Id, new ColumnSet(true));
                                }
                                increasedcostofworking["lux_cover"] = new OptionSetValue(972970005);
                                increasedcostofworking["lux_limitofindemnity"] = Limit;
                                increasedcostofworking["lux_limitofindemnityformatted"] = Limit.ToString("#,##0.00");
                                increasedcostofworking["transactioncurrencyid"] = new EntityReference("transactioncurrency", cpeQuote.GetAttributeValue<EntityReference>("transactioncurrencyid").Id);
                                increasedcostofworking["lux_riskcurrency"] = new EntityReference("transactioncurrency", cpeQuote.GetAttributeValue<EntityReference>("lux_riskcurrency").Id);
                                increasedcostofworking["lux_phoenixquoteoption"] = new EntityReference("lux_phoenixquoteoption", QuoteOptionId);
                                if (Increasedcostofworking != null)
                                {
                                    organizationService.Update(increasedcostofworking);
                                }
                                else
                                {
                                    organizationService.Create(increasedcostofworking);
                                }
                            }
                            else
                            {
                                foreach (var item in RateItem.Where(x => x.GetAttributeValue<int>("lux_rownumber") == 6))
                                {
                                    organizationService.Delete("lux_phoenixquoteoptionlist", item.Id);
                                }
                            }

                            if (cpeQuote.Contains("lux_iscoverrequiredforterrorism") && cpeQuote.GetAttributeValue<bool>("lux_iscoverrequiredforterrorism") == true)
                            {
                                Entity terrorism = new Entity("lux_phoenixquoteoptionlist");
                                if (Terrorism != null)
                                {
                                    terrorism = organizationService.Retrieve("lux_phoenixquoteoptionlist", Terrorism.Id, new ColumnSet(true));
                                }
                                terrorism["lux_cover"] = new OptionSetValue(972970006);
                                terrorism["transactioncurrencyid"] = new EntityReference("transactioncurrency", cpeQuote.GetAttributeValue<EntityReference>("transactioncurrencyid").Id);
                                terrorism["lux_riskcurrency"] = new EntityReference("transactioncurrency", cpeQuote.GetAttributeValue<EntityReference>("lux_riskcurrency").Id);
                                terrorism["lux_phoenixquoteoption"] = new EntityReference("lux_phoenixquoteoption", QuoteOptionId);
                                if (Terrorism != null)
                                {
                                    organizationService.Update(terrorism);
                                }
                                else
                                {
                                    organizationService.Create(terrorism);
                                }
                            }
                            else
                            {
                                foreach (var item in RateItem.Where(x => x.GetAttributeValue<int>("lux_rownumber") == 7))
                                {
                                    organizationService.Delete("lux_phoenixquoteoptionlist", item.Id);
                                }
                            }

                            var MaxContractPrice = cpeQuote.Contains("lux_maximumcontractprice") ? cpeQuote.GetAttributeValue<Money>("lux_maximumcontractprice").Value : 0;

                            Entity maximumContractPrice = new Entity("lux_phoenixquoteoptionlist");
                            if (MaximumContractPrice != null)
                            {
                                maximumContractPrice = organizationService.Retrieve("lux_phoenixquoteoptionlist", MaximumContractPrice.Id, new ColumnSet(true));
                            }
                            maximumContractPrice["lux_cover"] = new OptionSetValue(972970008);
                            maximumContractPrice["lux_limitofindemnity"] = MaxContractPrice;
                            maximumContractPrice["lux_limitofindemnityformatted"] = MaxContractPrice.ToString("#,##0.00");
                            maximumContractPrice["transactioncurrencyid"] = new EntityReference("transactioncurrency", cpeQuote.GetAttributeValue<EntityReference>("transactioncurrencyid").Id);
                            maximumContractPrice["lux_riskcurrency"] = new EntityReference("transactioncurrency", cpeQuote.GetAttributeValue<EntityReference>("lux_riskcurrency").Id);
                            maximumContractPrice["lux_phoenixquoteoption"] = new EntityReference("lux_phoenixquoteoption", QuoteOptionId);

                            if (MaximumContractPrice != null)
                            {
                                organizationService.Update(maximumContractPrice);
                            }
                            else
                            {
                                organizationService.Create(maximumContractPrice);
                            }

                            var riskInfo = organizationService.Retrieve("lux_globalcarriskinfo", cpeQuote.GetAttributeValue<EntityReference>("lux_globalcarriskinfo").Id, new ColumnSet(true));

                            if (riskInfo.Contains("lux_pleaseconfirmifcoverforoffsitestorageisre") && riskInfo.GetAttributeValue<bool>("lux_pleaseconfirmifcoverforoffsitestorageisre") == true)
                            {
                                var NonFerr = riskInfo.Contains("lux_nonferrousmetals") ? riskInfo.GetAttributeValue<decimal>("lux_nonferrousmetals") : 0;
                                var AllOther = riskInfo.Contains("lux_allothermaterials") ? riskInfo.GetAttributeValue<decimal>("lux_allothermaterials") : 0;
                                var Limit = NonFerr + AllOther;

                                var NonFerrExcess = riskInfo.Contains("lux_nonferrousmetalsexcess") ? riskInfo.GetAttributeValue<decimal>("lux_nonferrousmetalsexcess") : 0;

                                var OffSiteExcessFetch = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                                                          <entity name='lux_globaloffsitedeductible'>
                                                            <attribute name='lux_name' />
                                                            <attribute name='lux_deductibletype' />
                                                            <attribute name='lux_amountview' />
                                                            <attribute name='lux_minimumamount' />
                                                            <attribute name='lux_globaloffsitedeductibleid' />
                                                            <order attribute='lux_name' descending='false' />
                                                            <filter type='and'>
                                                              <condition attribute='statecode' operator='eq' value='0' />
                                                              <condition attribute='lux_contractorsplantandequipmentquote' operator='eq' uiname='' uitype='lux_contractorsplantandequipmentquote' value='{cpeQuote.Id}' />
                                                            </filter>
                                                          </entity>
                                                        </fetch>";

                                var OffSiteExcessList = organizationService.RetrieveMultiple(new FetchExpression(OffSiteExcessFetch)).Entities;
                                var OffSiteExcess = OffSiteExcessList.Sum(x => x.Contains("lux_amountview") ? x.GetAttributeValue<Money>("lux_amountview").Value : 0);
                                var totalExcess = NonFerrExcess + OffSiteExcess;

                                //MPL += cpeQuote.Contains("lux_ownplantanyoneoccurrence") ? cpeQuote.GetAttributeValue<decimal>("lux_ownplantanyoneoccurrence") : 0;

                                Entity offSiteStorage = new Entity("lux_phoenixquoteoptionlist");
                                if (OffSiteStorage != null)
                                {
                                    offSiteStorage = organizationService.Retrieve("lux_phoenixquoteoptionlist", OffSiteStorage.Id, new ColumnSet(true));
                                }
                                offSiteStorage["lux_cover"] = new OptionSetValue(972970007);
                                offSiteStorage["lux_limitofindemnity"] = Limit;
                                offSiteStorage["lux_limitofindemnityformatted"] = Limit.ToString("#,##0.00");
                                offSiteStorage["lux_excessformatted"] = totalExcess.ToString("#,##0.00");
                                offSiteStorage["transactioncurrencyid"] = new EntityReference("transactioncurrency", cpeQuote.GetAttributeValue<EntityReference>("transactioncurrencyid").Id);
                                offSiteStorage["lux_riskcurrency"] = new EntityReference("transactioncurrency", cpeQuote.GetAttributeValue<EntityReference>("lux_riskcurrency").Id);
                                offSiteStorage["lux_phoenixquoteoption"] = new EntityReference("lux_phoenixquoteoption", QuoteOptionId);

                                if (OffSiteStorage != null)
                                {
                                    organizationService.Update(offSiteStorage);
                                }
                                else
                                {
                                    organizationService.Create(offSiteStorage);
                                }
                            }
                            else
                            {
                                foreach (var item in RateItem.Where(x => x.GetAttributeValue<int>("lux_rownumber") == 8))
                                {
                                    organizationService.Delete("lux_phoenixquoteoptionlist", item.Id);
                                }
                            }

                            if (riskInfo.Contains("lux_pleaseconfirmifcoverforshowpropertiescont") && riskInfo.GetAttributeValue<bool>("lux_pleaseconfirmifcoverforshowpropertiescont") == true)
                            {
                                var Limit = riskInfo.Contains("lux_showpropertiescontentssuminsured") ? riskInfo.GetAttributeValue<decimal>("lux_showpropertiescontentssuminsured") : 0;
                                var PropertyContentExcessFetch = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                                                                      <entity name='lux_globalpropertiesdeductible'>
                                                                        <attribute name='lux_name' />
                                                                        <attribute name='lux_deductibletype' />
                                                                        <attribute name='lux_amountview' />
                                                                        <attribute name='lux_minimumamount' />
                                                                        <attribute name='lux_globalpropertiesdeductibleid' />
                                                                        <order attribute='lux_deductibletype' descending='false' />
                                                                        <filter type='and'>
                                                                          <condition attribute='statecode' operator='eq' value='0' />
                                                                          <condition attribute='lux_globalcarriskinfo' operator='eq' uiname='' uitype='lux_globalcarriskinfo' value='{riskInfo.Id}' />
                                                                        </filter>
                                                                      </entity>
                                                                    </fetch>";

                                var PropertyContentExcessList = organizationService.RetrieveMultiple(new FetchExpression(PropertyContentExcessFetch)).Entities;
                                var PropertyContentExcess = PropertyContentExcessList.Sum(x => x.Contains("lux_amountview") ? x.GetAttributeValue<Money>("lux_amountview").Value : 0);

                                var OtherExcessFetch = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                                                                      <entity name='lux_globalcarpropertiesotherdeductible'>
                                                                        <attribute name='lux_minimumamount' />
                                                                        <attribute name='lux_deductibletype' />
                                                                        <attribute name='lux_deductibletitlestandard' />
                                                                        <attribute name='lux_amountview' />
                                                                        <attribute name='lux_minimumamounttheft' />
                                                                        <attribute name='lux_deductibletypetheft' />
                                                                        <attribute name='lux_deductibletitletheft' />
                                                                        <attribute name='lux_amountviewtheft' />
                                                                        <attribute name='lux_minimumamountflood' />
                                                                        <attribute name='lux_deductibletypeflood' />
                                                                        <attribute name='lux_deductibletitleflood' />
                                                                        <attribute name='lux_amountviewflood' />
                                                                        <attribute name='lux_minimumamountescape' />
                                                                        <attribute name='lux_deductibletypeescape' />
                                                                        <attribute name='lux_deductibletitleescape' />
                                                                        <attribute name='lux_amountviewescape' />
                                                                        <attribute name='lux_minimumamountdefective' />
                                                                        <attribute name='lux_deductibletypedefective' />
                                                                        <attribute name='lux_deductibletitledefective' />
                                                                        <attribute name='lux_amountviewdefective' />
                                                                        <attribute name='lux_globalcarpropertiesotherdeductibleid' />
                                                                        <order attribute='lux_deductibletype' descending='false' />
                                                                        <filter type='and'>
                                                                          <condition attribute='statecode' operator='eq' value='0' />
                                                                          <condition attribute='lux_globalcarriskinfo' operator='eq' uiname='' uitype='lux_globalcarriskinfo' value='{riskInfo.Id}' />
                                                                        </filter>
                                                                      </entity>
                                                                    </fetch>";

                                var OtherExcessList = organizationService.RetrieveMultiple(new FetchExpression(OtherExcessFetch)).Entities;
                                var OtherExcess = OtherExcessList.Sum(x => x.Contains("lux_amountview") ? x.GetAttributeValue<Money>("lux_amountview").Value : 0);

                                var totalExcess = PropertyContentExcess + OtherExcess;

                                //MPL += cpeQuote.Contains("lux_ownplantanyoneoccurrence") ? cpeQuote.GetAttributeValue<decimal>("lux_ownplantanyoneoccurrence") : 0;

                                Entity showPropertiesContents = new Entity("lux_phoenixquoteoptionlist");
                                if (ShowPropertiesContents != null)
                                {
                                    showPropertiesContents = organizationService.Retrieve("lux_phoenixquoteoptionlist", ShowPropertiesContents.Id, new ColumnSet(true));
                                }
                                showPropertiesContents["lux_cover"] = new OptionSetValue(972970010);
                                showPropertiesContents["lux_limitofindemnity"] = Limit;
                                showPropertiesContents["lux_limitofindemnityformatted"] = Limit.ToString("#,##0.00");
                                showPropertiesContents["lux_excessformatted"] = totalExcess.ToString("#,##0.00");
                                showPropertiesContents["transactioncurrencyid"] = new EntityReference("transactioncurrency", cpeQuote.GetAttributeValue<EntityReference>("transactioncurrencyid").Id);
                                showPropertiesContents["lux_riskcurrency"] = new EntityReference("transactioncurrency", cpeQuote.GetAttributeValue<EntityReference>("lux_riskcurrency").Id);
                                showPropertiesContents["lux_phoenixquoteoption"] = new EntityReference("lux_phoenixquoteoption", QuoteOptionId);

                                if (ShowPropertiesContents != null)
                                {
                                    organizationService.Update(showPropertiesContents);
                                }
                                else
                                {
                                    organizationService.Create(showPropertiesContents);
                                }
                            }
                            else
                            {
                                foreach (var item in RateItem.Where(x => x.GetAttributeValue<int>("lux_rownumber") == 11))
                                {
                                    organizationService.Delete("lux_phoenixquoteoptionlist", item.Id);
                                }
                            }

                            if (riskInfo.Contains("lux_pleaseconfirmifcoverforexistingstructures") && riskInfo.GetAttributeValue<bool>("lux_pleaseconfirmifcoverforexistingstructures") == true)
                            {
                                var Limit = riskInfo.Contains("lux_totalexistingstructuressuminsured") ? riskInfo.GetAttributeValue<decimal>("lux_totalexistingstructuressuminsured") : 0;

                                //MPL += cpeQuote.Contains("lux_ownplantanyoneoccurrence") ? cpeQuote.GetAttributeValue<decimal>("lux_ownplantanyoneoccurrence") : 0;

                                Entity existingStructures = new Entity("lux_phoenixquoteoptionlist");
                                if (ExistingStructures != null)
                                {
                                    existingStructures = organizationService.Retrieve("lux_phoenixquoteoptionlist", ExistingStructures.Id, new ColumnSet(true));
                                }
                                existingStructures["lux_cover"] = new OptionSetValue(972970009);
                                existingStructures["lux_limitofindemnity"] = Limit;
                                existingStructures["lux_limitofindemnityformatted"] = Limit.ToString("#,##0.00");
                                existingStructures["transactioncurrencyid"] = new EntityReference("transactioncurrency", cpeQuote.GetAttributeValue<EntityReference>("transactioncurrencyid").Id);
                                existingStructures["lux_riskcurrency"] = new EntityReference("transactioncurrency", cpeQuote.GetAttributeValue<EntityReference>("lux_riskcurrency").Id);
                                existingStructures["lux_phoenixquoteoption"] = new EntityReference("lux_phoenixquoteoption", QuoteOptionId);

                                if (ExistingStructures != null)
                                {
                                    organizationService.Update(existingStructures);
                                }
                                else
                                {
                                    organizationService.Create(existingStructures);
                                }
                            }
                            else
                            {
                                foreach (var item in RateItem.Where(x => x.GetAttributeValue<int>("lux_rownumber") == 10))
                                {
                                    organizationService.Delete("lux_phoenixquoteoptionlist", item.Id);
                                }
                            }

                            if (riskInfo.Contains("lux_pleaseconfirmifcoverforthirdpartyliabilit") && riskInfo.GetAttributeValue<bool>("lux_pleaseconfirmifcoverforthirdpartyliabilit") == true)
                            {
                                var Limit = riskInfo.Contains("lux_thirdpartyliabilitylimitofindemnity") ? riskInfo.GetAttributeValue<decimal>("lux_thirdpartyliabilitylimitofindemnity") : 0;
                                var thirdPartyExcessFetch = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                                                                      <entity name='lux_globaltpldeductible'>
                                                                        <attribute name='lux_amountview' />
                                                                        <attribute name='lux_name' />
                                                                        <attribute name='lux_deductibletype' />
                                                                        <attribute name='lux_minimumamount' />
                                                                        <attribute name='lux_globaltpldeductibleid' />
                                                                        <order attribute='lux_deductibletype' descending='false' />
                                                                        <filter type='and'>
                                                                          <condition attribute='statecode' operator='eq' value='0' />
                                                                          <condition attribute='lux_globalcarriskinfo' operator='eq' uiname='' uitype='lux_globalcarriskinfo' value='{riskInfo.Id}' />
                                                                        </filter>
                                                                      </entity>
                                                                    </fetch>";

                                var thirdPartyExcessList = organizationService.RetrieveMultiple(new FetchExpression(thirdPartyExcessFetch)).Entities;
                                var totalExcess = thirdPartyExcessList.Sum(x => x.Contains("lux_amountview") ? x.GetAttributeValue<Money>("lux_amountview").Value : 0);

                                //MPL += cpeQuote.Contains("lux_ownplantanyoneoccurrence") ? cpeQuote.GetAttributeValue<decimal>("lux_ownplantanyoneoccurrence") : 0;

                                Entity thirdPartyLiability = new Entity("lux_phoenixquoteoptionlist");
                                if (ThirdPartyLiability != null)
                                {
                                    thirdPartyLiability = organizationService.Retrieve("lux_phoenixquoteoptionlist", ThirdPartyLiability.Id, new ColumnSet(true));
                                }
                                thirdPartyLiability["lux_cover"] = new OptionSetValue(972970011);
                                thirdPartyLiability["lux_limitofindemnity"] = Limit;
                                thirdPartyLiability["lux_limitofindemnityformatted"] = Limit.ToString("#,##0.00");
                                thirdPartyLiability["lux_excessformatted"] = totalExcess.ToString("#,##0.00");
                                thirdPartyLiability["transactioncurrencyid"] = new EntityReference("transactioncurrency", cpeQuote.GetAttributeValue<EntityReference>("transactioncurrencyid").Id);
                                thirdPartyLiability["lux_riskcurrency"] = new EntityReference("transactioncurrency", cpeQuote.GetAttributeValue<EntityReference>("lux_riskcurrency").Id);
                                thirdPartyLiability["lux_phoenixquoteoption"] = new EntityReference("lux_phoenixquoteoption", QuoteOptionId);

                                if (ThirdPartyLiability != null)
                                {
                                    organizationService.Update(thirdPartyLiability);
                                }
                                else
                                {
                                    organizationService.Create(thirdPartyLiability);
                                }
                            }
                            else
                            {
                                foreach (var item in RateItem.Where(x => x.GetAttributeValue<int>("lux_rownumber") == 12))
                                {
                                    organizationService.Delete("lux_phoenixquoteoptionlist", item.Id);
                                }
                            }

                            if (riskInfo.Contains("lux_pleaseconfirmifcoverfordelayinstartupisr") && riskInfo.GetAttributeValue<bool>("lux_pleaseconfirmifcoverfordelayinstartupisr") == true)
                            {
                                var Limit = riskInfo.Contains("lux_delayinstartuplimitofindemnity") ? riskInfo.GetAttributeValue<decimal>("lux_delayinstartuplimitofindemnity") : 0;
                                var delayExcessFetch = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                                                                      <entity name='lux_globaldelaydeductible'>
                                                                        <attribute name='lux_deductibletype' />
                                                                        <attribute name='lux_name' />
                                                                        <attribute name='lux_amountview' />
                                                                        <attribute name='lux_minimumamount' />
                                                                        <attribute name='lux_globaldelaydeductibleid' />
                                                                        <order attribute='lux_deductibletype' descending='false' />
                                                                        <filter type='and'>
                                                                          <condition attribute='statecode' operator='eq' value='0' />
                                                                          <condition attribute='lux_globalcarriskinfo' operator='eq' uiname='' uitype='lux_globalcarriskinfo' value='{riskInfo.Id}' />
                                                                        </filter>
                                                                      </entity>
                                                                    </fetch>";

                                var delayExcessList = organizationService.RetrieveMultiple(new FetchExpression(delayExcessFetch)).Entities;
                                var totalExcess = delayExcessList.Sum(x => x.Contains("lux_amountview") ? x.GetAttributeValue<Money>("lux_amountview").Value : 0);

                                //MPL += cpeQuote.Contains("lux_ownplantanyoneoccurrence") ? cpeQuote.GetAttributeValue<decimal>("lux_ownplantanyoneoccurrence") : 0;

                                Entity delayinstartup = new Entity("lux_phoenixquoteoptionlist");
                                if (Delayinstartup != null)
                                {
                                    delayinstartup = organizationService.Retrieve("lux_phoenixquoteoptionlist", Delayinstartup.Id, new ColumnSet(true));
                                }
                                delayinstartup["lux_cover"] = new OptionSetValue(972970012);
                                delayinstartup["lux_limitofindemnity"] = Limit;
                                delayinstartup["lux_limitofindemnityformatted"] = Limit.ToString("#,##0.00");
                                delayinstartup["lux_excessformatted"] = totalExcess.ToString("#,##0.00");
                                delayinstartup["transactioncurrencyid"] = new EntityReference("transactioncurrency", cpeQuote.GetAttributeValue<EntityReference>("transactioncurrencyid").Id);
                                delayinstartup["lux_riskcurrency"] = new EntityReference("transactioncurrency", cpeQuote.GetAttributeValue<EntityReference>("lux_riskcurrency").Id);
                                delayinstartup["lux_phoenixquoteoption"] = new EntityReference("lux_phoenixquoteoption", QuoteOptionId);

                                if (Delayinstartup != null)
                                {
                                    organizationService.Update(delayinstartup);
                                }
                                else
                                {
                                    organizationService.Create(delayinstartup);
                                }
                            }
                            else
                            {
                                foreach (var item in RateItem.Where(x => x.GetAttributeValue<int>("lux_rownumber") == 13))
                                {
                                    organizationService.Delete("lux_phoenixquoteoptionlist", item.Id);
                                }
                            }


                            if (cpeQuote.GetAttributeValue<OptionSetValue>("lux_carrier").Value != 972970001)
                            {
                                Entity cpeAppln = organizationService.Retrieve("lux_contractorsplantandequipmentquote", cpeQuote.Id, new ColumnSet(false));
                                cpeAppln["lux_totalmpl100"] = MPL;
                                organizationService.Update(cpeAppln);
                            }
                        }

                        if (quoteoption == "Yes")
                        {
                            if (OptionCount == 11)
                            {
                                throw new InvalidPluginExecutionException("More than 10 Quote Options can't be added");
                            }

                            //throw new InvalidPluginExecutionException(OptionCount.ToString());

                            if (!cpeQuote.Attributes.Contains("lux_quoteoption2") && OptionCount == 2)
                            {
                                Entity cpequoteoption = new Entity("lux_phoenixquoteoption");
                                cpequoteoption["lux_name"] = "Quote Option 2";
                                cpequoteoption["transactioncurrencyid"] = new EntityReference("transactioncurrency", cpeQuote.GetAttributeValue<EntityReference>("transactioncurrencyid").Id);
                                cpequoteoption["lux_contractorsplantandequipmentquote"] = new EntityReference("lux_contractorsplantandequipmentquote", cpeQuote.Id);

                                var QuoteoptionId = organizationService.Create(cpequoteoption);

                                Entity ptAppln = organizationService.Retrieve("lux_contractorsplantandequipmentquote", cpeQuote.Id, new ColumnSet(false));
                                ptAppln["lux_quoteoption2"] = new EntityReference("lux_phoenixquoteoption", QuoteoptionId);
                                organizationService.Update(ptAppln);
                            }

                            if (!cpeQuote.Attributes.Contains("lux_quoteoption3") && OptionCount == 3)
                            {
                                Entity cpequoteoption = new Entity("lux_phoenixquoteoption");
                                cpequoteoption["lux_name"] = "Quote Option 3";
                                cpequoteoption["transactioncurrencyid"] = new EntityReference("transactioncurrency", cpeQuote.GetAttributeValue<EntityReference>("transactioncurrencyid").Id);
                                cpequoteoption["lux_contractorsplantandequipmentquote"] = new EntityReference("lux_contractorsplantandequipmentquote", cpeQuote.Id);

                                var QuoteoptionId = organizationService.Create(cpequoteoption);

                                Entity ptAppln = organizationService.Retrieve("lux_contractorsplantandequipmentquote", cpeQuote.Id, new ColumnSet(false));
                                ptAppln["lux_quoteoption3"] = new EntityReference("lux_phoenixquoteoption", QuoteoptionId);
                                organizationService.Update(ptAppln);
                            }

                            if (!cpeQuote.Attributes.Contains("lux_quoteoption4") && OptionCount == 4)
                            {
                                Entity cpequoteoption = new Entity("lux_phoenixquoteoption");
                                cpequoteoption["lux_name"] = "Quote Option 4";
                                cpequoteoption["transactioncurrencyid"] = new EntityReference("transactioncurrency", cpeQuote.GetAttributeValue<EntityReference>("transactioncurrencyid").Id);
                                cpequoteoption["lux_contractorsplantandequipmentquote"] = new EntityReference("lux_contractorsplantandequipmentquote", cpeQuote.Id);

                                var QuoteoptionId = organizationService.Create(cpequoteoption);

                                Entity ptAppln = organizationService.Retrieve("lux_contractorsplantandequipmentquote", cpeQuote.Id, new ColumnSet(false));
                                ptAppln["lux_quoteoption4"] = new EntityReference("lux_phoenixquoteoption", QuoteoptionId);
                                organizationService.Update(ptAppln);
                            }

                            if (!cpeQuote.Attributes.Contains("lux_quoteoption5") && OptionCount == 5)
                            {
                                Entity cpequoteoption = new Entity("lux_phoenixquoteoption");
                                cpequoteoption["lux_name"] = "Quote Option 5";
                                cpequoteoption["transactioncurrencyid"] = new EntityReference("transactioncurrency", cpeQuote.GetAttributeValue<EntityReference>("transactioncurrencyid").Id);
                                cpequoteoption["lux_contractorsplantandequipmentquote"] = new EntityReference("lux_contractorsplantandequipmentquote", cpeQuote.Id);

                                var QuoteoptionId = organizationService.Create(cpequoteoption);

                                Entity ptAppln = organizationService.Retrieve("lux_contractorsplantandequipmentquote", cpeQuote.Id, new ColumnSet(false));
                                ptAppln["lux_quoteoption5"] = new EntityReference("lux_phoenixquoteoption", QuoteoptionId);
                                organizationService.Update(ptAppln);
                            }

                            if (!cpeQuote.Attributes.Contains("lux_quoteoption6") && OptionCount == 6)
                            {
                                Entity cpequoteoption = new Entity("lux_phoenixquoteoption");
                                cpequoteoption["lux_name"] = "Quote Option 6";
                                cpequoteoption["transactioncurrencyid"] = new EntityReference("transactioncurrency", cpeQuote.GetAttributeValue<EntityReference>("transactioncurrencyid").Id);
                                cpequoteoption["lux_contractorsplantandequipmentquote"] = new EntityReference("lux_contractorsplantandequipmentquote", cpeQuote.Id);

                                var QuoteoptionId = organizationService.Create(cpequoteoption);

                                Entity ptAppln = organizationService.Retrieve("lux_contractorsplantandequipmentquote", cpeQuote.Id, new ColumnSet(false));
                                ptAppln["lux_quoteoption6"] = new EntityReference("lux_phoenixquoteoption", QuoteoptionId);
                                organizationService.Update(ptAppln);
                            }

                            if (!cpeQuote.Attributes.Contains("lux_quoteoption7") && OptionCount == 7)
                            {
                                Entity cpequoteoption = new Entity("lux_phoenixquoteoption");
                                cpequoteoption["lux_name"] = "Quote Option 7";
                                cpequoteoption["transactioncurrencyid"] = new EntityReference("transactioncurrency", cpeQuote.GetAttributeValue<EntityReference>("transactioncurrencyid").Id);
                                cpequoteoption["lux_contractorsplantandequipmentquote"] = new EntityReference("lux_contractorsplantandequipmentquote", cpeQuote.Id);

                                var QuoteoptionId = organizationService.Create(cpequoteoption);

                                Entity ptAppln = organizationService.Retrieve("lux_contractorsplantandequipmentquote", cpeQuote.Id, new ColumnSet(false));
                                ptAppln["lux_quoteoption7"] = new EntityReference("lux_phoenixquoteoption", QuoteoptionId);
                                organizationService.Update(ptAppln);
                            }

                            if (!cpeQuote.Attributes.Contains("lux_quoteoption8") && OptionCount == 8)
                            {
                                Entity cpequoteoption = new Entity("lux_phoenixquoteoption");
                                cpequoteoption["lux_name"] = "Quote Option 8";
                                cpequoteoption["transactioncurrencyid"] = new EntityReference("transactioncurrency", cpeQuote.GetAttributeValue<EntityReference>("transactioncurrencyid").Id);
                                cpequoteoption["lux_contractorsplantandequipmentquote"] = new EntityReference("lux_contractorsplantandequipmentquote", cpeQuote.Id);

                                var QuoteoptionId = organizationService.Create(cpequoteoption);

                                Entity ptAppln = organizationService.Retrieve("lux_contractorsplantandequipmentquote", cpeQuote.Id, new ColumnSet(false));
                                ptAppln["lux_quoteoption8"] = new EntityReference("lux_phoenixquoteoption", QuoteoptionId);
                                organizationService.Update(ptAppln);
                            }

                            if (!cpeQuote.Attributes.Contains("lux_quoteoption9") && OptionCount == 9)
                            {
                                Entity cpequoteoption = new Entity("lux_phoenixquoteoption");
                                cpequoteoption["lux_name"] = "Quote Option 9";
                                cpequoteoption["transactioncurrencyid"] = new EntityReference("transactioncurrency", cpeQuote.GetAttributeValue<EntityReference>("transactioncurrencyid").Id);
                                cpequoteoption["lux_contractorsplantandequipmentquote"] = new EntityReference("lux_contractorsplantandequipmentquote", cpeQuote.Id);

                                var QuoteoptionId = organizationService.Create(cpequoteoption);

                                Entity ptAppln = organizationService.Retrieve("lux_contractorsplantandequipmentquote", cpeQuote.Id, new ColumnSet(false));
                                ptAppln["lux_quoteoption9"] = new EntityReference("lux_phoenixquoteoption", QuoteoptionId);
                                organizationService.Update(ptAppln);
                            }

                            if (!cpeQuote.Attributes.Contains("lux_quoteoption10") && OptionCount == 10)
                            {
                                Entity cpequoteoption = new Entity("lux_phoenixquoteoption");
                                cpequoteoption["lux_name"] = "Quote Option 10";
                                cpequoteoption["transactioncurrencyid"] = new EntityReference("transactioncurrency", cpeQuote.GetAttributeValue<EntityReference>("transactioncurrencyid").Id);
                                cpequoteoption["lux_contractorsplantandequipmentquote"] = new EntityReference("lux_contractorsplantandequipmentquote", cpeQuote.Id);

                                var QuoteoptionId = organizationService.Create(cpequoteoption);

                                Entity ptAppln = organizationService.Retrieve("lux_contractorsplantandequipmentquote", cpeQuote.Id, new ColumnSet(false));
                                ptAppln["lux_quoteoption10"] = new EntityReference("lux_phoenixquoteoption", QuoteoptionId);
                                organizationService.Update(ptAppln);
                            }
                        }
                        else
                        {
                            if (cpeQuote.Attributes.Contains("lux_quoteoption1"))
                            {
                                Entity Appln = organizationService.Retrieve("lux_contractorsplantandequipmentquote", cpeQuote.Id, new ColumnSet(false));
                                Appln["lux_quoteoptionselected"] = new EntityReference("lux_phoenixquoteoption", QuoteOptionId);
                                Appln["lux_quoteoptionscount"] = 1;
                                organizationService.Update(Appln);
                            }

                            if (cpeQuote.Attributes.Contains("lux_quoteoption2"))
                            {
                                organizationService.Delete("lux_phoenixquoteoption", cpeQuote.GetAttributeValue<EntityReference>("lux_quoteoption2").Id);
                            }
                            if (cpeQuote.Attributes.Contains("lux_quoteoption3"))
                            {
                                organizationService.Delete("lux_phoenixquoteoption", cpeQuote.GetAttributeValue<EntityReference>("lux_quoteoption3").Id);
                            }
                            if (cpeQuote.Attributes.Contains("lux_quoteoption4"))
                            {
                                organizationService.Delete("lux_phoenixquoteoption", cpeQuote.GetAttributeValue<EntityReference>("lux_quoteoption4").Id);
                            }
                            if (cpeQuote.Attributes.Contains("lux_quoteoption5"))
                            {
                                organizationService.Delete("lux_phoenixquoteoption", cpeQuote.GetAttributeValue<EntityReference>("lux_quoteoption5").Id);
                            }
                            if (cpeQuote.Attributes.Contains("lux_quoteoption6"))
                            {
                                organizationService.Delete("lux_phoenixquoteoption", cpeQuote.GetAttributeValue<EntityReference>("lux_quoteoption6").Id);
                            }
                            if (cpeQuote.Attributes.Contains("lux_quoteoption7"))
                            {
                                organizationService.Delete("lux_phoenixquoteoption", cpeQuote.GetAttributeValue<EntityReference>("lux_quoteoption7").Id);
                            }
                            if (cpeQuote.Attributes.Contains("lux_quoteoption8"))
                            {
                                organizationService.Delete("lux_phoenixquoteoption", cpeQuote.GetAttributeValue<EntityReference>("lux_quoteoption8").Id);
                            }
                            if (cpeQuote.Attributes.Contains("lux_quoteoption9"))
                            {
                                organizationService.Delete("lux_phoenixquoteoption", cpeQuote.GetAttributeValue<EntityReference>("lux_quoteoption9").Id);
                            }
                            if (cpeQuote.Attributes.Contains("lux_quoteoption10"))
                            {
                                organizationService.Delete("lux_phoenixquoteoption", cpeQuote.GetAttributeValue<EntityReference>("lux_quoteoption10").Id);
                            }

                            Entity ptAppln = organizationService.Retrieve("lux_contractorsplantandequipmentquote", cpeQuote.Id, new ColumnSet(false));
                            ptAppln["lux_quoteoptionscount"] = 1;
                            organizationService.Update(ptAppln);
                        }
                    }

                    if (entity.LogicalName == "lux_phoenixquoteoptionlist")
                    {
                        var TradeRow = organizationService.Retrieve(entity.LogicalName, entity.Id, new ColumnSet(true));
                        var cpeQuoteOption = organizationService.Retrieve("lux_phoenixquoteoption", TradeRow.GetAttributeValue<EntityReference>("lux_phoenixquoteoption").Id, new ColumnSet("lux_contractorsplantandequipmentquote"));
                        cpeQuote = organizationService.Retrieve("lux_contractorsplantandequipmentquote", cpeQuoteOption.GetAttributeValue<EntityReference>("lux_contractorsplantandequipmentquote").Id, new ColumnSet("lux_product"));
                        Product = cpeQuote.FormattedValues["lux_product"];

                        if (Product == "Contractors Plant and Equipment" || Product == "Contractors All Risk")
                        {
                            var tradeCover = TradeRow.Contains("lux_cover") ? TradeRow.GetAttributeValue<OptionSetValue>("lux_cover").Value : 0;
                            var tradeExcess = TradeRow.Contains("lux_excess") ? TradeRow.GetAttributeValue<decimal>("lux_excess") : 0M;
                            var tradeLimit = TradeRow.Contains("lux_limitofindemnity") ? TradeRow.GetAttributeValue<decimal>("lux_limitofindemnity") : 0M;

                            var tradefetch = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                                              <entity name='lux_phoenixquoteoptionlist'>
                                                <attribute name='createdon' />
                                                <attribute name='lux_limitofindemnity' />
                                                <attribute name='lux_excess' />
                                                <attribute name='lux_cover' />
                                                <attribute name='lux_phoenixquoteoptionlistid' />
                                                <attribute name='lux_phoenixquoteoption' />
                                                <order attribute='createdon' descending='false' />
                                                <filter type='and'>
                                                  <condition attribute='statecode' operator='eq' value='0' />
                                                  <condition attribute='lux_phoenixquoteoptionlistid' operator='ne' uiname='' uitype='lux_phoenixquoteoptionlist' value='{TradeRow.Id}' />
                                                </filter>
                                                <link-entity name='lux_phoenixquoteoption' from='lux_phoenixquoteoptionid' to='lux_phoenixquoteoption' link-type='inner' alias='ab'>
                                                  <filter type='and'>
                                                    <condition attribute='lux_contractorsplantandequipmentquote' operator='eq' uiname='' uitype='lux_contractorsplantandequipmentquote' value='{cpeQuote.Id}' />
                                                  </filter>
                                                </link-entity>
                                              </entity>
                                            </fetch>";

                            var tradefetch1 = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                                              <entity name='lux_phoenixquoteoptionlist'>
                                                <attribute name='createdon' />
                                                <attribute name='lux_limitofindemnity' />
                                                <attribute name='lux_excess' />
                                                <attribute name='lux_cover' />
                                                <attribute name='lux_phoenixquoteoptionlistid' />
                                                <attribute name='lux_phoenixquoteoption' />
                                                <order attribute='createdon' descending='false' />
                                                <filter type='and'>
                                                  <condition attribute='statecode' operator='eq' value='0' />
                                                  <condition attribute='lux_phoenixquoteoption' operator='eq' uiname='' uitype='lux_phoenixquoteoption' value='{cpeQuoteOption.Id}' />
                                                  <condition attribute='lux_phoenixquoteoptionlistid' operator='ne' uiname='' uitype='lux_phoenixquoteoptionlist' value='{TradeRow.Id}' />
                                                </filter>
                                              </entity>
                                            </fetch>";

                            var tradeList = organizationService.RetrieveMultiple(new FetchExpression(tradefetch));
                            var tradeList1 = organizationService.RetrieveMultiple(new FetchExpression(tradefetch1));

                            foreach (var item in tradeList1.Entities)
                            {
                                if (item.GetAttributeValue<OptionSetValue>("lux_cover").Value == tradeCover)
                                {
                                    //throw new InvalidPluginExecutionException(item.GetAttributeValue<OptionSetValue>("lux_cover").Value.ToString() + ", " + tradeCover);
                                    throw new InvalidPluginExecutionException("You can not add more than 1 cover type of the same drop down per quote option");
                                }
                            }

                            var TradeRow1 = organizationService.Retrieve(entity.LogicalName, entity.Id, new ColumnSet(false));
                            TradeRow1["lux_limitofindemnityformatted"] = tradeLimit.ToString("#,##0.00");
                            TradeRow1["lux_excessformatted"] = tradeExcess.ToString("#,##0.00");
                            organizationService.Update(TradeRow1);
                        }
                    }
                }
                catch (Exception)
                {
                    throw;
                }
            }
        }
    }
}