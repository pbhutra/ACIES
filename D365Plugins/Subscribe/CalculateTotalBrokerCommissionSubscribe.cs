using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using System;
using System.Linq;

namespace D365Plugins
{
    public class CalculateTotalBrokerCommissionSubscribe : IPlugin
    {
        public void Execute(IServiceProvider serviceProvider)
        {
            IPluginExecutionContext context = (IPluginExecutionContext)serviceProvider.GetService(typeof(IPluginExecutionContext));
            ITracingService tracingService = (ITracingService)serviceProvider.GetService(typeof(ITracingService));

            if (context.InputParameters.Contains("Target") && context.Depth <= 2)
            {
                try
                {
                    // Obtain the organization service reference.
                    IOrganizationServiceFactory serviceFactory = (IOrganizationServiceFactory)serviceProvider.GetService(typeof(IOrganizationServiceFactory));
                    IOrganizationService organizationService = serviceFactory.CreateOrganizationService(context.UserId);

                    Entity entity = (Entity)context.InputParameters["Target"];

                    var subsQuoteOption = organizationService.Retrieve("lux_subscribequoteoption", entity.Id, new ColumnSet("lux_technicalpremiumbeforetax", "lux_policypremiumbeforetax", "lux_subscribeprofessionalindemnityquote"));
                    var subsQuote = organizationService.Retrieve("lux_subscribepiquote", subsQuoteOption.GetAttributeValue<EntityReference>("lux_subscribeprofessionalindemnityquote").Id, new ColumnSet(true));

                    var TechnicalPremium = subsQuoteOption.Attributes.Contains("lux_technicalpremiumbeforetax") ? subsQuoteOption.GetAttributeValue<Money>("lux_technicalpremiumbeforetax").Value : 0;
                    var PolicyPremium = subsQuoteOption.Attributes.Contains("lux_policypremiumbeforetax") ? subsQuoteOption.GetAttributeValue<Money>("lux_policypremiumbeforetax").Value : 0;

                    var Brokerfetch = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
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

                    var Taxfetch = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                                                  <entity name='lux_subscribequotetaxtype'>
                                                    <attribute name='lux_name' />
                                                    <attribute name='lux_taxpercentage' />
                                                    <attribute name='lux_subscribequotetaxtypeid' />
                                                    <filter type='and'>
                                                      <condition attribute='statecode' operator='eq' value='0' />
                                                      <condition attribute='lux_subscribequoteoption' operator='eq' uiname='' uitype='lux_subscribequoteoption' value='{subsQuoteOption.Id}' />
                                                    </filter>
                                                  </entity>
                                                </fetch>";

                    var taxList = organizationService.RetrieveMultiple(new FetchExpression(Taxfetch));

                    var TechnicalTaxRate = 0M;
                    var PolicyTaxRate = 0M;

                    if (taxList.Entities.Count() > 0)
                    {
                        TechnicalTaxRate = taxList.Entities.Sum(x => x.Attributes.Contains("lux_taxpercentage") ? x.GetAttributeValue<decimal>("lux_taxpercentage") : 0);
                        PolicyTaxRate = taxList.Entities.Sum(x => x.Attributes.Contains("lux_taxpercentage") ? x.GetAttributeValue<decimal>("lux_taxpercentage") : 0);

                        Entity application = organizationService.Retrieve("lux_subscribequoteoption", subsQuoteOption.Id, new ColumnSet(false));
                        application["lux_technicaltotaltaxamount"] = new Money(TechnicalPremium * TechnicalTaxRate / 100);
                        application["lux_policytotaltaxamount"] = new Money(PolicyPremium * PolicyTaxRate / 100);
                        application["lux_policytotaltax"] = PolicyTaxRate;
                        organizationService.Update(application);
                    }

                    var subsBrokerList = organizationService.RetrieveMultiple(new FetchExpression(Brokerfetch));
                    if (subsBrokerList.Entities.Count() > 0)
                    {
                        var TotalCommPercentage = subsBrokerList.Entities.Where(x => x.GetAttributeValue<OptionSetValue>("lux_percentageorflatfee").Value == 972970001).Sum(x => x.Attributes.Contains("lux_percentage") ? x.GetAttributeValue<decimal>("lux_percentage") : 0M);
                        var TotalFlatFee = subsBrokerList.Entities.Where(x => x.GetAttributeValue<OptionSetValue>("lux_percentageorflatfee").Value == 972970002).Sum(x => x.Attributes.Contains("lux_commissonamount") ? x.GetAttributeValue<Money>("lux_commissonamount").Value : 0M);

                        var TotalTechnicalBrokerCommAmt = TechnicalPremium * (TotalCommPercentage) / 100 + TotalFlatFee;
                        var TotalPolicyBrokerCommAmt = PolicyPremium * (TotalCommPercentage) / 100 + TotalFlatFee;

                        Entity quoteOption = organizationService.Retrieve("lux_subscribequoteoption", subsQuoteOption.Id, new ColumnSet(false));

                        if (TechnicalPremium != 0)
                        {
                            quoteOption["lux_technicalbrokercommissionpercentage"] = TotalTechnicalBrokerCommAmt * 100 / TechnicalPremium;
                            quoteOption["lux_technicalbrokercommissionamount"] = new Money(TotalTechnicalBrokerCommAmt);
                        }
                        else
                        {
                            quoteOption["lux_technicalbrokercommissionpercentage"] = TotalCommPercentage;
                        }

                        if (PolicyPremium != 0)
                        {
                            quoteOption["lux_policybrokercommissionpercentage"] = TotalPolicyBrokerCommAmt * 100 / PolicyPremium;
                            quoteOption["lux_policybrokercommissionamount"] = new Money(TotalPolicyBrokerCommAmt);
                        }
                        else
                        {
                            quoteOption["lux_policybrokercommissionpercentage"] = TotalCommPercentage;
                        }

                        organizationService.Update(quoteOption);
                        //throw new InvalidPluginExecutionException(CommissionAmount.ToString());
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