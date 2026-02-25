using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using System;
using System.Linq;

namespace D365Plugins
{
    public class CalculateTotalBrokerSubscribe : IPlugin
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

                    var PremiumRow = organizationService.Retrieve("lux_subscribebrokersagent", entity.Id, new ColumnSet(true));
                    var subsQuote = organizationService.Retrieve("lux_subscribepiquote", PremiumRow.GetAttributeValue<EntityReference>("lux_subscribepiquote").Id, new ColumnSet(true));
                    var quoteoption = subsQuote.Attributes.Contains("lux_wouldyouliketooffermultiplequoteoptions") ? subsQuote.FormattedValues["lux_wouldyouliketooffermultiplequoteoptions"] : "No";
                    var OptionCount = subsQuote.Contains("lux_quoteoptionscount") ? subsQuote.GetAttributeValue<int>("lux_quoteoptionscount") : 0;

                    var FinalRatingfetch = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                                                  <entity name='lux_subscribebrokersagent'>
                                                    <attribute name='lux_percentageorflatfee' />
                                                    <attribute name='lux_percentage' />
                                                    <attribute name='lux_product' />
                                                    <attribute name='lux_broker' />
                                                    <attribute name='lux_commissonamount' />
                                                    <attribute name='lux_subscribebrokersagentid' />
                                                    <order attribute='lux_name' descending='false' />
                                                    <filter type='and'>
                                                      <condition attribute='statecode' operator='eq' value='0' />
                                                      <condition attribute='lux_subscribepiquote' operator='eq' uiname='' uitype='lux_subscribepiquote' value='{subsQuote.Id}' />
                                                    </filter>
                                                  </entity>
                                                </fetch>";

                    if (context.MessageName == "Delete")
                    {
                        FinalRatingfetch = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                                                  <entity name='lux_subscribebrokersagent'>
                                                    <attribute name='lux_percentageorflatfee' />
                                                    <attribute name='lux_percentage' />
                                                    <attribute name='lux_product' />
                                                    <attribute name='lux_broker' />
                                                    <attribute name='lux_commissonamount' />
                                                    <attribute name='lux_subscribebrokersagentid' />
                                                    <order attribute='lux_name' descending='false' />
                                                    <filter type='and'>
                                                      <condition attribute='statecode' operator='eq' value='0' />
                                                      <condition attribute='lux_subscribepiquote' operator='eq' uiname='' uitype='lux_subscribepiquote' value='{subsQuote.Id}' />
                                                      <condition attribute='lux_subscribebrokersagentid' operator='ne' uiname='' uitype='lux_subscribebrokersagent' value='{entity.Id}' />
                                                    </filter>
                                                  </entity>
                                                </fetch>";
                    }

                    var subsList = organizationService.RetrieveMultiple(new FetchExpression(FinalRatingfetch));
                    if (subsList.Entities.Count() > 0)
                    {
                        var TotalCommPercentage = subsList.Entities.Where(x => x.GetAttributeValue<OptionSetValue>("lux_percentageorflatfee").Value == 972970001).Sum(x => x.Attributes.Contains("lux_percentage") ? x.GetAttributeValue<decimal>("lux_percentage") : 0M);
                        var TotalFlatFee = subsList.Entities.Where(x => x.GetAttributeValue<OptionSetValue>("lux_percentageorflatfee").Value == 972970002).Sum(x => x.Attributes.Contains("lux_commissonamount") ? x.GetAttributeValue<Money>("lux_commissonamount").Value : 0M);

                        //throw new InvalidPluginExecutionException(TotalCommPercentage.ToString());

                        if (quoteoption == "Yes")
                        {
                            //if (subsQuote.Attributes.Contains("lux_quoteoption1"))
                            //{
                            //    Entity quoteOption = organizationService.Retrieve("lux_subscribequoteoption", subsQuote.GetAttributeValue<EntityReference>("lux_quoteoption1").Id, new ColumnSet("lux_technicalpremiumbeforetax", "lux_policypremiumbeforetax"));

                            //    var TechnicalPremium = quoteOption.Attributes.Contains("lux_technicalpremiumbeforetax") ? quoteOption.GetAttributeValue<Money>("lux_technicalpremiumbeforetax").Value : 0;
                            //    var PolicyPremium = quoteOption.Attributes.Contains("lux_policypremiumbeforetax") ? quoteOption.GetAttributeValue<Money>("lux_policypremiumbeforetax").Value : TechnicalPremium;

                            //    var TotalTechnicalBrokerCommAmt = TechnicalPremium * (TotalCommPercentage) / 100 + TotalFlatFee;
                            //    var TotalPolicyBrokerCommAmt = PolicyPremium * (TotalCommPercentage) / 100 + TotalFlatFee;

                            //    if (TechnicalPremium != 0)
                            //    {
                            //        quoteOption["lux_technicalbrokercommissionpercentage"] = TotalTechnicalBrokerCommAmt * 100 / TechnicalPremium;
                            //        quoteOption["lux_technicalbrokercommissionamount"] = new Money(TotalTechnicalBrokerCommAmt);
                            //    }
                            //    else
                            //    {
                            //        quoteOption["lux_technicalbrokercommissionpercentage"] = TotalCommPercentage;
                            //    }

                            //    if (PolicyPremium != 0)
                            //    {
                            //        quoteOption["lux_policybrokercommissionpercentage"] = TotalPolicyBrokerCommAmt * 100 / PolicyPremium;
                            //        quoteOption["lux_policybrokercommissionamount"] = new Money(TotalPolicyBrokerCommAmt);
                            //    }
                            //    else
                            //    {
                            //        quoteOption["lux_policybrokercommissionpercentage"] = TotalCommPercentage;
                            //    }

                            //    organizationService.Update(quoteOption);
                            //}


                            if (OptionCount >= 1)
                            {
                                for (int i = 1; i <= OptionCount; i++)
                                {
                                    Entity quoteOption1 = organizationService.Retrieve("lux_subscribequoteoption", subsQuote.GetAttributeValue<EntityReference>("lux_quoteoption" + i).Id, new ColumnSet("lux_technicalpremiumbeforetax", "lux_policypremiumbeforetax"));

                                    var TechnicalPremium1 = quoteOption1.Attributes.Contains("lux_technicalpremiumbeforetax") ? quoteOption1.GetAttributeValue<Money>("lux_technicalpremiumbeforetax").Value : 0;
                                    var PolicyPremium1 = quoteOption1.Attributes.Contains("lux_policypremiumbeforetax") ? quoteOption1.GetAttributeValue<Money>("lux_policypremiumbeforetax").Value : TechnicalPremium1;

                                    var TotalTechnicalBrokerCommAmt = TechnicalPremium1 * (TotalCommPercentage) / 100 + TotalFlatFee;
                                    var TotalPolicyBrokerCommAmt = PolicyPremium1 * (TotalCommPercentage) / 100 + TotalFlatFee;

                                    if (TechnicalPremium1 != 0)
                                    {
                                        quoteOption1["lux_technicalbrokercommissionpercentage"] = TotalTechnicalBrokerCommAmt * 100 / TechnicalPremium1;
                                        quoteOption1["lux_technicalbrokercommissionamount"] = new Money(TotalTechnicalBrokerCommAmt);
                                    }
                                    else
                                    {
                                        quoteOption1["lux_technicalbrokercommissionpercentage"] = TotalCommPercentage;
                                    }

                                    if (PolicyPremium1 != 0)
                                    {
                                        quoteOption1["lux_policybrokercommissionpercentage"] = TotalPolicyBrokerCommAmt * 100 / PolicyPremium1;
                                        quoteOption1["lux_policybrokercommissionamount"] = new Money(TotalPolicyBrokerCommAmt);
                                    }
                                    else
                                    {
                                        quoteOption1["lux_policybrokercommissionpercentage"] = TotalCommPercentage;
                                    }
                                    organizationService.Update(quoteOption1);
                                }
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