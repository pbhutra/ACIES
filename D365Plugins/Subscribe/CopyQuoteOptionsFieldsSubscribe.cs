using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using System;
using System.Linq;

namespace D365Plugins
{
    public class CopyQuoteOptionsFieldsSubscribe : IPlugin
    {
        public void Execute(IServiceProvider serviceProvider)
        {
            IPluginExecutionContext context = (IPluginExecutionContext)serviceProvider.GetService(typeof(IPluginExecutionContext));
            ITracingService tracingService = (ITracingService)serviceProvider.GetService(typeof(ITracingService));
            if (context.InputParameters.Contains("Target") && context.Depth == 1)
            {
                try
                {
                    // Obtain the organization service reference.
                    IOrganizationServiceFactory serviceFactory = (IOrganizationServiceFactory)serviceProvider.GetService(typeof(IOrganizationServiceFactory));
                    IOrganizationService organizationService = serviceFactory.CreateOrganizationService(context.UserId);

                    // Obtain the target entity from the input parameters.
                    Entity entity = new Entity();
                    entity = (Entity)context.InputParameters["Target"];

                    var subsPolicy = organizationService.Retrieve(entity.LogicalName, entity.Id, new ColumnSet(true));
                    var subsQuote = organizationService.Retrieve("lux_subscribepiquote", subsPolicy.GetAttributeValue<EntityReference>("lux_subscribequote").Id, new ColumnSet("lux_wouldyouliketooffermultiplequoteoptions", "lux_quoteoptions", "lux_product", "lux_subscribemlriskinfo"));

                    var quoteoption = subsQuote.Attributes.Contains("lux_wouldyouliketooffermultiplequoteoptions") ? subsQuote.FormattedValues["lux_wouldyouliketooffermultiplequoteoptions"] : "No";
                    var Product = subsQuote.FormattedValues["lux_product"];

                    if (quoteoption == "Yes")
                    {
                        var selectedQuoteOption = organizationService.Retrieve("lux_subscribequoteoption", subsQuote.GetAttributeValue<EntityReference>("lux_quoteoptions").Id, new ColumnSet(true));

                        if (Product == "Professional Indemnity")
                        {
                            var tradefetch1 = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                                          <entity name='lux_subscribepiquoteoptionlist'>
                                            <attribute name='lux_pilimitofindemnity' />
                                            <attribute name='lux_excess' />
                                            <attribute name='lux_limitofindemnity' />
                                            <attribute name='lux_rownumber' />
                                            <attribute name='lux_subscribepiquoteoptionlistid' />
                                            <order attribute='lux_rownumber' descending='false' />
                                            <filter type='and'>
                                              <condition attribute='statecode' operator='eq' value='0' />
                                              <condition attribute='lux_subscribequoteoption' operator='eq' uiname='' uitype='lux_subscribequoteoption' value='{selectedQuoteOption.Id}' />
                                            </filter>
                                          </entity>
                                        </fetch>";

                            var tradeList1 = organizationService.RetrieveMultiple(new FetchExpression(tradefetch1));

                            var PICover = tradeList1.Entities.FirstOrDefault(x => x.GetAttributeValue<int>("lux_rownumber") == 1);
                            var FidilityCover = tradeList1.Entities.FirstOrDefault(x => x.GetAttributeValue<int>("lux_rownumber") == 2);
                            var PLCover = tradeList1.Entities.FirstOrDefault(x => x.GetAttributeValue<int>("lux_rownumber") == 3);
                            var ELCover = tradeList1.Entities.FirstOrDefault(x => x.GetAttributeValue<int>("lux_rownumber") == 4);

                            if (PICover != null)
                            {
                                subsQuote["lux_pleaseselecttheprofessionalindemnitythein"] = new OptionSetValue(PICover.GetAttributeValue<OptionSetValue>("lux_pilimitofindemnity").Value);
                                subsQuote["lux_piotherlimit"] = PICover.GetAttributeValue<decimal>("lux_limitofindemnity");
                                subsQuote["lux_excessrequired"] = PICover.GetAttributeValue<decimal>("lux_excess");
                            }
                            else
                            {
                                subsQuote["lux_pleaseselecttheprofessionalindemnitythein"] = null;
                                subsQuote["lux_piotherlimit"] = new decimal(0);
                                subsQuote["lux_excessrequired"] = new decimal(0);
                            }

                            if (FidilityCover != null)
                            {
                                subsQuote["lux_iscoverforfidelityrequired"] = true;
                                subsQuote["lux_fidelitycoverlimitofindemnity"] = FidilityCover.GetAttributeValue<decimal>("lux_limitofindemnity");
                                subsQuote["lux_fidelitycoverexcess"] = FidilityCover.GetAttributeValue<decimal>("lux_excess");
                            }
                            else
                            {
                                subsQuote["lux_iscoverforfidelityrequired"] = false;
                                subsQuote["lux_fidelitycoverlimitofindemnity"] = new decimal(0);
                                subsQuote["lux_fidelitycoverexcess"] = new decimal(0);
                            }

                            if (PLCover != null)
                            {
                                subsQuote["lux_iscoverforpublicliabilityrequired"] = true;
                                subsQuote["lux_pleaseselectthelimitofliabilitytheinsure"] = new OptionSetValue(PLCover.GetAttributeValue<OptionSetValue>("lux_pilimitofindemnity").Value);
                                subsQuote["lux_ifotherlimitrequiredpleaseprovidedetails"] = PLCover.GetAttributeValue<decimal>("lux_limitofindemnity");
                                subsQuote["lux_publicliabilityexcess"] = PLCover.GetAttributeValue<decimal>("lux_excess");
                            }
                            else
                            {
                                subsQuote["lux_iscoverforpublicliabilityrequired"] = false;
                                subsQuote["lux_pleaseselectthelimitofliabilitytheinsure"] = null;
                                subsQuote["lux_ifotherlimitrequiredpleaseprovidedetails"] = new decimal(0);
                                subsQuote["lux_publicliabilityexcess"] = new decimal(0);
                            }

                            if (ELCover != null)
                            {
                                subsQuote["lux_iscoverforemployersliabilityrequired"] = true;
                                subsQuote["lux_employersliabilitylimitofliability"] = ELCover.GetAttributeValue<decimal>("lux_limitofindemnity");
                            }
                            else
                            {
                                subsQuote["lux_iscoverforemployersliabilityrequired"] = false;
                                subsQuote["lux_employersliabilitylimitofliability"] = new decimal(0);
                            }
                            organizationService.Update(subsQuote);
                        }
                        else if (Product == "Management Liability")
                        {
                            var RiskRow = organizationService.Retrieve("lux_subscribemlriskinfo", subsQuote.GetAttributeValue<EntityReference>("lux_subscribemlriskinfo").Id, new ColumnSet(false));
                            var Ratingfetch = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                                          <entity name='lux_subscribepiquoteoptionlist'>
                                            <attribute name='lux_mllimitofindemnity' />
                                            <attribute name='lux_excess' />
                                            <attribute name='lux_limitofindemnity' />
                                            <attribute name='lux_rownumber' />
                                            <attribute name='lux_subscribepiquoteoptionlistid' />
                                            <order attribute='lux_rownumber' descending='false' />
                                            <filter type='and'>
                                              <condition attribute='statecode' operator='eq' value='0' />
                                              <condition attribute='lux_subscribequoteoption' operator='eq' uiname='' uitype='lux_subscribequoteoption' value='{selectedQuoteOption.Id}' />
                                            </filter>
                                          </entity>
                                        </fetch>";

                            var RateItem = organizationService.RetrieveMultiple(new FetchExpression(Ratingfetch)).Entities;

                            var DirectorsandOfficersLiability = RateItem.FirstOrDefault(x => x.GetAttributeValue<int>("lux_rownumber") == 1);
                            var EntityLiability = RateItem.FirstOrDefault(x => x.GetAttributeValue<int>("lux_rownumber") == 2);
                            var EmploymentPracticeLiability = RateItem.FirstOrDefault(x => x.GetAttributeValue<int>("lux_rownumber") == 3);
                            var Crime = RateItem.FirstOrDefault(x => x.GetAttributeValue<int>("lux_rownumber") == 4);
                            var TrusteeCover = RateItem.FirstOrDefault(x => x.GetAttributeValue<int>("lux_rownumber") == 5);
                            var StatutoryLiability = RateItem.FirstOrDefault(x => x.GetAttributeValue<int>("lux_rownumber") == 6);

                            if (DirectorsandOfficersLiability != null)
                            {
                                RiskRow["lux_directorsandofficersliabilitylimitofindem"] = new OptionSetValue(DirectorsandOfficersLiability.GetAttributeValue<OptionSetValue>("lux_mllimitofindemnity").Value - 3);
                                RiskRow["lux_ifotherlimitpleasespecify"] = DirectorsandOfficersLiability.GetAttributeValue<decimal>("lux_limitofindemnity");
                                RiskRow["lux_directorsandofficersliabilityexcess"] = DirectorsandOfficersLiability.GetAttributeValue<decimal>("lux_excess");
                            }
                            else
                            {
                                RiskRow["lux_directorsandofficersliabilitylimitofindem"] = null;
                                RiskRow["lux_ifotherlimitpleasespecify"] = new decimal(0);
                                RiskRow["lux_directorsandofficersliabilityexcess"] = new decimal(0);
                            }

                            if (EntityLiability != null)
                            {
                                RiskRow["lux_isentityliabilitycoverrequired"] = true;
                                RiskRow["lux_entityliabilitylimitofindemnity"] = new OptionSetValue(EntityLiability.GetAttributeValue<OptionSetValue>("lux_mllimitofindemnity").Value - 3);
                                RiskRow["lux_ifotherlimitpleasespecifyentityliability"] = EntityLiability.GetAttributeValue<decimal>("lux_limitofindemnity");
                                RiskRow["lux_entityliabilitylimitexcess"] = EntityLiability.GetAttributeValue<decimal>("lux_excess");
                            }
                            else
                            {
                                RiskRow["lux_isentityliabilitycoverrequired"] = false;
                                RiskRow["lux_entityliabilitylimitofindemnity"] = null;
                                RiskRow["lux_ifotherlimitpleasespecifyentityliability"] = new decimal(0);
                                RiskRow["lux_entityliabilitylimitexcess"] = new decimal(0);
                            }

                            if (EmploymentPracticeLiability != null)
                            {
                                RiskRow["lux_iscoverforemployementpracticeliabilityreq"] = true;
                                RiskRow["lux_employmentpracticeliabilitylimitofindemni"] = new OptionSetValue(EmploymentPracticeLiability.GetAttributeValue<OptionSetValue>("lux_mllimitofindemnity").Value - 3);
                                RiskRow["lux_ifotherlimitpleasespecifyemployeepratice"] = EmploymentPracticeLiability.GetAttributeValue<decimal>("lux_limitofindemnity");
                                RiskRow["lux_employementpracticeliabilityexcess"] = EmploymentPracticeLiability.GetAttributeValue<decimal>("lux_excess");
                            }
                            else
                            {
                                RiskRow["lux_iscoverforemployementpracticeliabilityreq"] = false;
                                RiskRow["lux_employmentpracticeliabilitylimitofindemni"] = null;
                                RiskRow["lux_ifotherlimitpleasespecifyemployeepratice"] = new decimal(0);
                                RiskRow["lux_employementpracticeliabilityexcess"] = new decimal(0);
                            }

                            if (Crime != null)
                            {
                                RiskRow["lux_iscoverforcrimerequired"] = true;

                                if (Crime.GetAttributeValue<OptionSetValue>("lux_mllimitofindemnity").Value == 972970002)
                                {
                                    RiskRow["lux_crimelimitofindemnity"] = new OptionSetValue(972970001);
                                }
                                else if (Crime.GetAttributeValue<OptionSetValue>("lux_mllimitofindemnity").Value == 972970003)
                                {
                                    RiskRow["lux_crimelimitofindemnity"] = new OptionSetValue(972970002);
                                }
                                else if (Crime.GetAttributeValue<OptionSetValue>("lux_mllimitofindemnity").Value == 972970004)
                                {
                                    RiskRow["lux_crimelimitofindemnity"] = new OptionSetValue(972970003);
                                }
                                else if (Crime.GetAttributeValue<OptionSetValue>("lux_mllimitofindemnity").Value == 972970008)
                                {
                                    RiskRow["lux_crimelimitofindemnity"] = new OptionSetValue(972970004);
                                }
                                RiskRow["lux_ifotherlimitpleasespecifycrimecover"] = Crime.GetAttributeValue<decimal>("lux_limitofindemnity");
                                RiskRow["lux_crimecoverexcess"] = Crime.GetAttributeValue<decimal>("lux_excess");
                            }
                            else
                            {
                                RiskRow["lux_iscoverforcrimerequired"] = false;
                                RiskRow["lux_crimelimitofindemnity"] = null;
                                RiskRow["lux_ifotherlimitpleasespecifycrimecover"] = new decimal(0);
                                RiskRow["lux_crimecoverexcess"] = new decimal(0);
                            }

                            if (TrusteeCover != null)
                            {
                                RiskRow["lux_istrusteecoverrequired"] = true;

                                if (TrusteeCover.GetAttributeValue<OptionSetValue>("lux_mllimitofindemnity").Value == 972970002)
                                {
                                    RiskRow["lux_trusteelimitofindemnity"] = new OptionSetValue(972970001);
                                }
                                else if (TrusteeCover.GetAttributeValue<OptionSetValue>("lux_mllimitofindemnity").Value == 972970003)
                                {
                                    RiskRow["lux_trusteelimitofindemnity"] = new OptionSetValue(972970002);
                                }
                                else if (TrusteeCover.GetAttributeValue<OptionSetValue>("lux_mllimitofindemnity").Value == 972970004)
                                {
                                    RiskRow["lux_trusteelimitofindemnity"] = new OptionSetValue(972970003);
                                }
                                else if (TrusteeCover.GetAttributeValue<OptionSetValue>("lux_mllimitofindemnity").Value == 972970008)
                                {
                                    RiskRow["lux_trusteelimitofindemnity"] = new OptionSetValue(972970004);
                                }
                                RiskRow["lux_ifotherlimitpleasespecifytrusteelimit"] = TrusteeCover.GetAttributeValue<decimal>("lux_limitofindemnity");
                                RiskRow["lux_trusteecoverexcess"] = TrusteeCover.GetAttributeValue<decimal>("lux_excess");
                            }
                            else
                            {
                                RiskRow["lux_istrusteecoverrequired"] = false;
                                RiskRow["lux_trusteelimitofindemnity"] = null;
                                RiskRow["lux_ifotherlimitpleasespecifytrusteelimit"] = new decimal(0);
                                RiskRow["lux_trusteecoverexcess"] = new decimal(0);
                            }

                            if (StatutoryLiability != null)
                            {
                                RiskRow["lux_statutoryliabilitycoverrequired"] = true;

                                if (StatutoryLiability.GetAttributeValue<OptionSetValue>("lux_mllimitofindemnity").Value == 972970008)
                                {
                                    RiskRow["lux_statutoryliabilitylimitofindemnity"] = new OptionSetValue(972970004);
                                }
                                else
                                {
                                    RiskRow["lux_statutoryliabilitylimitofindemnity"] = new OptionSetValue(StatutoryLiability.GetAttributeValue<OptionSetValue>("lux_mllimitofindemnity").Value);
                                }
                                RiskRow["lux_ifotherlimitpleasespecifystatutoryliabili"] = StatutoryLiability.GetAttributeValue<decimal>("lux_limitofindemnity");
                                RiskRow["lux_statutoryliabilityexcess"] = StatutoryLiability.GetAttributeValue<decimal>("lux_excess");
                            }
                            else
                            {
                                RiskRow["lux_statutoryliabilitycoverrequired"] = false;
                                RiskRow["lux_statutoryliabilitylimitofindemnity"] = null;
                                RiskRow["lux_ifotherlimitpleasespecifystatutoryliabili"] = new decimal(0);
                                RiskRow["lux_statutoryliabilityexcess"] = new decimal(0);
                            }

                            organizationService.Update(RiskRow);
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