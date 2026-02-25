using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using System;
using System.Linq;

namespace D365Plugins
{
    public class CalculatePremiumSubscribe : IPlugin
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

                    EntityReference e = (EntityReference)context.InputParameters["Target"];
                    entity = organizationService.Retrieve(e.LogicalName, e.Id, new ColumnSet(true));

                    var subsQuote = organizationService.Retrieve(entity.LogicalName, entity.Id, new ColumnSet(true));
                    var Product = subsQuote.FormattedValues["lux_product"];

                    if (Product == "Professional Indemnity")
                    {
                        var quoteOptionfetch = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                                                  <entity name='lux_subscribequoteoption'>
                                                    <attribute name='lux_subscribequoteoptionid' />
                                                    <attribute name='lux_name' />
                                                    <attribute name='createdon' />
                                                    <order attribute='lux_name' descending='false' />
                                                    <filter type='and'>
                                                      <condition attribute='statecode' operator='eq' value='0' />
                                                      <condition attribute='lux_subscribeprofessionalindemnityquote' operator='eq' uiname='' uitype='lux_subscribepiquote' value='{subsQuote.Id}' />
                                                    </filter>
                                                  </entity>
                                                </fetch>";

                        var quoteOptionsList = organizationService.RetrieveMultiple(new FetchExpression(quoteOptionfetch));

                        String[][] array3d = new String[quoteOptionsList.Entities.Count][];
                        var j = 0;

                        if (quoteOptionsList.Entities.Count > 1)
                        {
                            foreach (var item in quoteOptionsList.Entities)
                            {
                                var optionlistFetch = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                                              <entity name='lux_subscribepiquoteoptionlist'>
                                                <attribute name='createdon' />
                                                <attribute name='lux_limitofindemnity' />
                                                <attribute name='lux_excess' />
                                                <attribute name='lux_cover' />
                                                <attribute name='lux_subscribepiquoteoptionlistid' />
                                                <attribute name='lux_subscribequoteoption' />
                                                <order attribute='lux_rownumber' descending='false' />
                                                <filter type='and'>
                                                  <condition attribute='statecode' operator='eq' value='0' />
                                                  <condition attribute='lux_subscribequoteoption' operator='eq' uiname='' uitype='lux_subscribequoteoption' value='{item.Id}' />
                                                </filter>
                                              </entity>
                                            </fetch>";

                                var optionlistList = organizationService.RetrieveMultiple(new FetchExpression(optionlistFetch));
                                String[] array3d1 = new String[optionlistList.Entities.Count];
                                var i = 0;
                                foreach (var listitem in optionlistList.Entities)
                                {
                                    array3d1.SetValue(listitem.GetAttributeValue<OptionSetValue>("lux_cover").Value + "," + listitem.GetAttributeValue<decimal>("lux_limitofindemnity").ToString("#.00") + "," + listitem.GetAttributeValue<decimal>("lux_excess").ToString("#.00"), i);
                                    i++;
                                }

                                for (int k = 0; k < array3d.Distinct().Count() - 1; k++)
                                {
                                    if (array3d[k].SequenceEqual(array3d1))
                                    {
                                        throw new InvalidPluginExecutionException("You can not add cover type of the same Limit and same Excess");
                                    }
                                }
                                array3d.SetValue(array3d1, j);
                                j++;
                            }
                        }

                        var quoteoption = subsQuote.Attributes.Contains("lux_wouldyouliketooffermultiplequoteoptions") ? subsQuote.FormattedValues["lux_wouldyouliketooffermultiplequoteoptions"] : "No";
                        var OptionCount = subsQuote.Contains("lux_quoteoptionscount") ? subsQuote.GetAttributeValue<int>("lux_quoteoptionscount") : 0;
                        var primaryTrade = organizationService.Retrieve("lux_subscribepitrade", subsQuote.GetAttributeValue<EntityReference>("lux_primarytrade").Id, new ColumnSet(true));
                        var Country = subsQuote.Attributes.Contains("lux_registeredaddresscountry") ? subsQuote.Attributes["lux_registeredaddresscountry"].ToString() : "";

                        var PIRiskCode = primaryTrade.Contains("lux_professionalindemnitynonusariskcode") ? primaryTrade.Attributes["lux_professionalindemnitynonusariskcode"].ToString() : "";
                        var PLRiskCode = primaryTrade.Contains("lux_publicliabilitynonusariskcode") ? primaryTrade.Attributes["lux_publicliabilitynonusariskcode"].ToString() : "";
                        var ELRiskCode = "";

                        if (Country.Contains("United States"))
                        {
                            PIRiskCode = primaryTrade.Contains("lux_professionalindemnityusariskcode") ? primaryTrade.Attributes["lux_professionalindemnityusariskcode"].ToString() : "";
                            PLRiskCode = primaryTrade.Contains("lux_publicliabilityusariskcode") ? primaryTrade.Attributes["lux_publicliabilityusariskcode"].ToString() : "";
                        }

                        if (Country.Contains("United Kingdom"))
                        {
                            ELRiskCode = primaryTrade.Contains("lux_employersliabilityukriskcode") ? primaryTrade.Attributes["lux_employersliabilityukriskcode"].ToString() : "";
                        }

                        var tradefetch1 = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                                          <entity name='lux_subscribepisectionpremium'>
                                            <attribute name='lux_sectionreference' />
                                            <attribute name='lux_technicalpremium' />
                                            <attribute name='lux_ratingfigures' />
                                            <attribute name='lux_ratedeviationpercentage' />
                                            <attribute name='lux_policypremium' />
                                            <attribute name='lux_justificaiton' />
                                            <attribute name='lux_discountorloadingpercentage' />
                                            <attribute name='lux_comment' />
                                            <attribute name='lux_section' />
                                            <attribute name='lux_subscribepisectionpremiumid' />
                                            <order attribute='lux_sectionreference' descending='false' />
                                            <filter type='and'>
                                              <condition attribute='statecode' operator='eq' value='0' />
                                            </filter>
                                            <link-entity name='lux_subscribequoteoption' from='lux_subscribequoteoptionid' to='lux_subscribequoteoption' link-type='inner' alias='ab'>
                                              <filter type='and'>
                                                <condition attribute='lux_subscribeprofessionalindemnityquote' operator='eq' uiname='' uitype='lux_subscribepiquote' value='{subsQuote.Id}' />
                                              </filter>
                                            </link-entity>
                                          </entity>
                                        </fetch>";

                        var tradeList1 = organizationService.RetrieveMultiple(new FetchExpression(tradefetch1));
                        foreach (var item in tradeList1.Entities)
                        {
                            organizationService.Delete("lux_subscribepisectionpremium", item.Id);
                        }

                        if (subsQuote.Attributes.Contains("lux_quoteoption1"))
                        {
                            var tradefetch = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                                              <entity name='lux_subscribepiquoteoptionlist'>
                                                <attribute name='createdon' />
                                                <attribute name='lux_limitofindemnity' />
                                                <attribute name='lux_excess' />
                                                <attribute name='lux_cover' />
                                                <attribute name='lux_subscribepiquoteoptionlistid' />
                                                <attribute name='lux_subscribequoteoption' />
                                                <order attribute='createdon' descending='false' />
                                                <filter type='and'>
                                                  <condition attribute='statecode' operator='eq' value='0' />
                                                </filter>
                                                <link-entity name='lux_subscribequoteoption' from='lux_subscribequoteoptionid' to='lux_subscribequoteoption' link-type='inner' alias='ab'>
                                                  <filter type='and'>
                                                    <condition attribute='lux_subscribeprofessionalindemnityquote' operator='eq' uiname='' uitype='lux_subscribepiquote' value='{subsQuote.Id}' />
                                                  </filter>
                                                </link-entity>
                                              </entity>
                                            </fetch>";

                            var tradeList = organizationService.RetrieveMultiple(new FetchExpression(tradefetch));
                            foreach (var item in tradeList.Entities)
                            {
                                var tradeCover = item.Contains("lux_cover") ? item.GetAttributeValue<OptionSetValue>("lux_cover").Value : 0;
                                var tradeExcess = item.Contains("lux_excess") ? item.GetAttributeValue<decimal>("lux_excess") : 0M;
                                var tradeLimit = item.Contains("lux_limitofindemnity") ? item.GetAttributeValue<decimal>("lux_limitofindemnity") : 0M;

                                Entity subscribepisectionpremium = new Entity("lux_subscribepisectionpremium");
                                subscribepisectionpremium["lux_section"] = new OptionSetValue(tradeCover);
                                if (tradeCover == 972970001)
                                {
                                    subscribepisectionpremium["lux_sectionreference"] = PIRiskCode;
                                }
                                else if (tradeCover == 972970003)
                                {
                                    subscribepisectionpremium["lux_sectionreference"] = PLRiskCode;
                                }
                                else if (tradeCover == 972970004)
                                {
                                    subscribepisectionpremium["lux_sectionreference"] = ELRiskCode;
                                }

                                subscribepisectionpremium["lux_ratingfigures"] = new Money(tradeLimit);
                                subscribepisectionpremium["transactioncurrencyid"] = new EntityReference("transactioncurrency", subsQuote.GetAttributeValue<EntityReference>("transactioncurrencyid").Id);
                                subscribepisectionpremium["lux_subscribequoteoption"] = new EntityReference("lux_subscribequoteoption", item.GetAttributeValue<EntityReference>("lux_subscribequoteoption").Id);
                                organizationService.Create(subscribepisectionpremium);
                            }

                            var TotalBrokerCommPercentage = 0M;
                            var brokerfetch = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                                                  <entity name='lux_subscribebrokersagent'>
                                                    <attribute name='lux_product' />
                                                    <attribute name='lux_percentageorflatfee' />
                                                    <attribute name='lux_comment' />
                                                    <attribute name='lux_broker' />
                                                    <attribute name='lux_percentage' />
                                                    <attribute name='lux_commissonamount' />
                                                    <attribute name='lux_subscribebrokersagentid' />
                                                    <order attribute='lux_broker' descending='false' />
                                                    <filter type='and'>
                                                      <condition attribute='statecode' operator='eq' value='0' />
                                                      <condition attribute='lux_subscribepiquote' operator='eq' uiname='SUBPI00010Q25' uitype='lux_subscribepiquote' value='{subsQuote.Id}' />
                                                    </filter>
                                                  </entity>
                                                </fetch>";

                            var brokerList = organizationService.RetrieveMultiple(new FetchExpression(brokerfetch));
                            if (brokerList.Entities.Count() > 0)
                            {
                                TotalBrokerCommPercentage = brokerList.Entities.Where(x => x.GetAttributeValue<OptionSetValue>("lux_percentageorflatfee").Value == 972970001).Sum(x => x.Attributes.Contains("lux_percentage") ? x.GetAttributeValue<decimal>("lux_percentage") : 0M);
                            }

                            var quoteoptionsfetch = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                                                          <entity name='lux_subscribequoteoption'>
                                                            <attribute name='lux_subscribequoteoptionid' />
                                                            <attribute name='lux_name' />
                                                            <attribute name='createdon' />
                                                            <order attribute='lux_name' descending='false' />
                                                            <filter type='and'>
                                                              <condition attribute='statecode' operator='eq' value='0' />
                                                              <condition attribute='lux_subscribeprofessionalindemnityquote' operator='eq' uiname='' uitype='lux_subscribepiquote' value='{subsQuote.Id}' />
                                                            </filter>
                                                          </entity>
                                                        </fetch>";

                            var quoteoptionList = organizationService.RetrieveMultiple(new FetchExpression(quoteoptionsfetch));
                            int count = 0;
                            foreach (var item in quoteoptionList.Entities)
                            {
                                Entity application = organizationService.Retrieve("lux_subscribequoteoption", item.Id, new ColumnSet(false));
                                application["lux_technicalbrokercommissionpercentage"] = TotalBrokerCommPercentage;
                                application["lux_policybrokercommissionpercentage"] = TotalBrokerCommPercentage;

                                application["lux_technicalmgacommissionpercentage"] = 15M;
                                application["lux_policymgacommissionpercentage"] = 15M;
                                organizationService.Update(application);
                                count++;
                            }
                        }
                    }
                    else if (Product == "Management Liability")
                    {
                        var quoteOptionfetch = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                                                  <entity name='lux_subscribequoteoption'>
                                                    <attribute name='lux_subscribequoteoptionid' />
                                                    <attribute name='lux_name' />
                                                    <attribute name='createdon' />
                                                    <order attribute='lux_name' descending='false' />
                                                    <filter type='and'>
                                                      <condition attribute='statecode' operator='eq' value='0' />
                                                      <condition attribute='lux_subscribeprofessionalindemnityquote' operator='eq' uiname='' uitype='lux_subscribepiquote' value='{subsQuote.Id}' />
                                                    </filter>
                                                  </entity>
                                                </fetch>";

                        var quoteOptionsList = organizationService.RetrieveMultiple(new FetchExpression(quoteOptionfetch));

                        String[][] array3d = new String[quoteOptionsList.Entities.Count][];
                        var j = 0;

                        if (quoteOptionsList.Entities.Count > 1)
                        {
                            foreach (var item in quoteOptionsList.Entities)
                            {
                                var optionlistFetch = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                                              <entity name='lux_subscribepiquoteoptionlist'>
                                                <attribute name='createdon' />
                                                <attribute name='lux_limitofindemnity' />
                                                <attribute name='lux_excess' />
                                                <attribute name='lux_mlcover' />
                                                <attribute name='lux_subscribepiquoteoptionlistid' />
                                                <attribute name='lux_subscribequoteoption' />
                                                <order attribute='lux_rownumber' descending='false' />
                                                <filter type='and'>
                                                  <condition attribute='statecode' operator='eq' value='0' />
                                                  <condition attribute='lux_subscribequoteoption' operator='eq' uiname='' uitype='lux_subscribequoteoption' value='{item.Id}' />
                                                </filter>
                                              </entity>
                                            </fetch>";

                                var optionlistList = organizationService.RetrieveMultiple(new FetchExpression(optionlistFetch));
                                String[] array3d1 = new String[optionlistList.Entities.Count];
                                var i = 0;
                                foreach (var listitem in optionlistList.Entities)
                                {
                                    array3d1.SetValue(listitem.GetAttributeValue<OptionSetValue>("lux_mlcover").Value + "," + listitem.GetAttributeValue<decimal>("lux_limitofindemnity").ToString("#.00") + "," + listitem.GetAttributeValue<decimal>("lux_excess").ToString("#.00"), i);
                                    i++;
                                }

                                for (int k = 0; k < array3d.Distinct().Count() - 1; k++)
                                {
                                    if (array3d[k].SequenceEqual(array3d1))
                                    {
                                        throw new InvalidPluginExecutionException("You can not add cover type of the same Limit and same Excess");
                                    }
                                }
                                array3d.SetValue(array3d1, j);
                                j++;
                            }
                        }

                        var quoteoption = subsQuote.Attributes.Contains("lux_wouldyouliketooffermultiplequoteoptions") ? subsQuote.FormattedValues["lux_wouldyouliketooffermultiplequoteoptions"] : "No";
                        var OptionCount = subsQuote.Contains("lux_quoteoptionscount") ? subsQuote.GetAttributeValue<int>("lux_quoteoptionscount") : 0;
                        var primaryTrade = organizationService.Retrieve("lux_subscribepitrade", subsQuote.GetAttributeValue<EntityReference>("lux_primarytrade").Id, new ColumnSet(true));
                        var Country = subsQuote.Attributes.Contains("lux_registeredaddresscountry") ? subsQuote.Attributes["lux_registeredaddresscountry"].ToString() : "";

                        var MLRiskCode = primaryTrade.Contains("lux_mlpnonusariskcode") ? primaryTrade.Attributes["lux_mlpnonusariskcode"].ToString() : "";

                        if (Country.Contains("United States"))
                        {
                            MLRiskCode = primaryTrade.Contains("lux_mlpusariskcode") ? primaryTrade.Attributes["lux_mlpusariskcode"].ToString() : "";
                        }

                        var tradefetch1 = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                                          <entity name='lux_subscribepisectionpremium'>
                                            <attribute name='lux_sectionreference' />
                                            <attribute name='lux_technicalpremium' />
                                            <attribute name='lux_ratingfigures' />
                                            <attribute name='lux_ratedeviationpercentage' />
                                            <attribute name='lux_policypremium' />
                                            <attribute name='lux_justificaiton' />
                                            <attribute name='lux_discountorloadingpercentage' />
                                            <attribute name='lux_comment' />
                                            <attribute name='lux_mlsection' />
                                            <attribute name='lux_subscribepisectionpremiumid' />
                                            <order attribute='lux_sectionreference' descending='false' />
                                            <filter type='and'>
                                              <condition attribute='statecode' operator='eq' value='0' />
                                            </filter>
                                            <link-entity name='lux_subscribequoteoption' from='lux_subscribequoteoptionid' to='lux_subscribequoteoption' link-type='inner' alias='ab'>
                                              <filter type='and'>
                                                <condition attribute='lux_subscribeprofessionalindemnityquote' operator='eq' uiname='' uitype='lux_subscribepiquote' value='{subsQuote.Id}' />
                                              </filter>
                                            </link-entity>
                                          </entity>
                                        </fetch>";

                        var tradeList1 = organizationService.RetrieveMultiple(new FetchExpression(tradefetch1));
                        foreach (var item in tradeList1.Entities)
                        {
                            organizationService.Delete("lux_subscribepisectionpremium", item.Id);
                        }

                        if (subsQuote.Attributes.Contains("lux_quoteoption1"))
                        {
                            var tradefetch = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                                              <entity name='lux_subscribepiquoteoptionlist'>
                                                <attribute name='createdon' />
                                                <attribute name='lux_limitofindemnity' />
                                                <attribute name='lux_excess' />
                                                <attribute name='lux_mlcover' />
                                                <attribute name='lux_subscribepiquoteoptionlistid' />
                                                <attribute name='lux_subscribequoteoption' />
                                                <order attribute='createdon' descending='false' />
                                                <filter type='and'>
                                                  <condition attribute='statecode' operator='eq' value='0' />
                                                </filter>
                                                <link-entity name='lux_subscribequoteoption' from='lux_subscribequoteoptionid' to='lux_subscribequoteoption' link-type='inner' alias='ab'>
                                                  <filter type='and'>
                                                    <condition attribute='lux_subscribeprofessionalindemnityquote' operator='eq' uiname='' uitype='lux_subscribepiquote' value='{subsQuote.Id}' />
                                                  </filter>
                                                </link-entity>
                                              </entity>
                                            </fetch>";

                            var tradeList = organizationService.RetrieveMultiple(new FetchExpression(tradefetch));
                            foreach (var item in tradeList.Entities)
                            {
                                var tradeCover = item.Contains("lux_mlcover") ? item.GetAttributeValue<OptionSetValue>("lux_mlcover").Value : 0;
                                var tradeExcess = item.Contains("lux_excess") ? item.GetAttributeValue<decimal>("lux_excess") : 0M;
                                var tradeLimit = item.Contains("lux_limitofindemnity") ? item.GetAttributeValue<decimal>("lux_limitofindemnity") : 0M;

                                Entity subscribepisectionpremium = new Entity("lux_subscribepisectionpremium");
                                subscribepisectionpremium["lux_mlsection"] = new OptionSetValue(tradeCover);
                                subscribepisectionpremium["lux_sectionreference"] = MLRiskCode;
                                subscribepisectionpremium["lux_ratingfigures"] = new Money(tradeLimit);
                                subscribepisectionpremium["transactioncurrencyid"] = new EntityReference("transactioncurrency", subsQuote.GetAttributeValue<EntityReference>("transactioncurrencyid").Id);
                                subscribepisectionpremium["lux_subscribequoteoption"] = new EntityReference("lux_subscribequoteoption", item.GetAttributeValue<EntityReference>("lux_subscribequoteoption").Id);
                                organizationService.Create(subscribepisectionpremium);
                            }

                            var TotalBrokerCommPercentage = 0M;
                            var brokerfetch = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                                                  <entity name='lux_subscribebrokersagent'>
                                                    <attribute name='lux_product' />
                                                    <attribute name='lux_percentageorflatfee' />
                                                    <attribute name='lux_comment' />
                                                    <attribute name='lux_broker' />
                                                    <attribute name='lux_percentage' />
                                                    <attribute name='lux_commissonamount' />
                                                    <attribute name='lux_subscribebrokersagentid' />
                                                    <order attribute='lux_broker' descending='false' />
                                                    <filter type='and'>
                                                      <condition attribute='statecode' operator='eq' value='0' />
                                                      <condition attribute='lux_subscribepiquote' operator='eq' uiname='' uitype='lux_subscribepiquote' value='{subsQuote.Id}' />
                                                    </filter>
                                                  </entity>
                                                </fetch>";

                            var brokerList = organizationService.RetrieveMultiple(new FetchExpression(brokerfetch));
                            if (brokerList.Entities.Count() > 0)
                            {
                                TotalBrokerCommPercentage = brokerList.Entities.Where(x => x.GetAttributeValue<OptionSetValue>("lux_percentageorflatfee").Value == 972970001).Sum(x => x.Attributes.Contains("lux_percentage") ? x.GetAttributeValue<decimal>("lux_percentage") : 0M);
                            }

                            var quoteoptionsfetch = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                                                          <entity name='lux_subscribequoteoption'>
                                                            <attribute name='lux_subscribequoteoptionid' />
                                                            <attribute name='lux_name' />
                                                            <attribute name='createdon' />
                                                            <order attribute='lux_name' descending='false' />
                                                            <filter type='and'>
                                                              <condition attribute='statecode' operator='eq' value='0' />
                                                              <condition attribute='lux_subscribeprofessionalindemnityquote' operator='eq' uiname='' uitype='lux_subscribepiquote' value='{subsQuote.Id}' />
                                                            </filter>
                                                          </entity>
                                                        </fetch>";

                            var quoteoptionList = organizationService.RetrieveMultiple(new FetchExpression(quoteoptionsfetch));
                            int count = 0;
                            foreach (var item in quoteoptionList.Entities)
                            {
                                Entity application = organizationService.Retrieve("lux_subscribequoteoption", item.Id, new ColumnSet(false));
                                application["lux_technicalbrokercommissionpercentage"] = TotalBrokerCommPercentage;
                                application["lux_policybrokercommissionpercentage"] = TotalBrokerCommPercentage;

                                application["lux_technicalmgacommissionpercentage"] = 15M;
                                application["lux_policymgacommissionpercentage"] = 15M;
                                organizationService.Update(application);
                                count++;
                                // if (count == 3)
                                //throw new InvalidPluginExecutionException(quoteoptionList.Entities.Count.ToString());
                            }
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