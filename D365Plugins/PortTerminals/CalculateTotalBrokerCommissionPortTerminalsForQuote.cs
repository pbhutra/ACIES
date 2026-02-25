using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using System;
using System.Linq;

namespace D365Plugins
{
    public class CalculateTotalBrokerCommissionPortTerminalsForQuote : IPlugin
    {
        public void Execute(IServiceProvider serviceProvider)
        {
            IPluginExecutionContext context = (IPluginExecutionContext)serviceProvider.GetService(typeof(IPluginExecutionContext));
            ITracingService tracingService = (ITracingService)serviceProvider.GetService(typeof(ITracingService));

            if (context.InputParameters.Contains("Target") &&  context.Depth <= 2)
            {
                try
                {
                    // Obtain the organization service reference.
                    IOrganizationServiceFactory serviceFactory = (IOrganizationServiceFactory)serviceProvider.GetService(typeof(IOrganizationServiceFactory));
                    IOrganizationService organizationService = serviceFactory.CreateOrganizationService(context.UserId);

                    Entity entity = (Entity)context.InputParameters["Target"];

                    var ptQuote = organizationService.Retrieve("lux_portandterminalsquote", entity.Id, new ColumnSet("lux_technicalpremiumbeforetax", "lux_policypremiumbeforetax"));

                    var TechnicalPremium = ptQuote.Attributes.Contains("lux_technicalpremiumbeforetax") ? ptQuote.GetAttributeValue<Money>("lux_technicalpremiumbeforetax").Value : 0;
                    var PolicyPremium = ptQuote.Attributes.Contains("lux_policypremiumbeforetax") ? ptQuote.GetAttributeValue<Money>("lux_policypremiumbeforetax").Value : 0;

                    var FinalRatingfetch = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                                                  <entity name='lux_portandterminalsbrokerpremium'>
                                                    <attribute name='lux_percentageamount' />
                                                    <attribute name='lux_percentageorflatfee' />
                                                    <attribute name='lux_commissonamount' />
                                                    <attribute name='lux_portandterminalsbrokerpremiumid' />
                                                    <order attribute='lux_name' descending='false' />
                                                    <filter type='and'>
                                                      <condition attribute='statecode' operator='eq' value='0' />
                                                      <condition attribute='lux_portandterminalsquote' operator='eq' uiname='' uitype='lux_portandterminalsquote' value='{ptQuote.Id}' />
                                                    </filter>
                                                  </entity>
                                                </fetch>";

                    var Taxfetch = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                                                  <entity name='lux_portandterminalquotetaxtype'>
                                                    <attribute name='lux_name' />
                                                    <attribute name='lux_taxpercentage' />
                                                    <attribute name='lux_portandterminalquotetaxtypeid' />
                                                    <filter type='and'>
                                                      <condition attribute='statecode' operator='eq' value='0' />
                                                      <condition attribute='lux_portandterminalsquote' operator='eq' uiname='' uitype='lux_portandterminalsquote' value='{ptQuote.Id}' />
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

                        Entity application = organizationService.Retrieve("lux_portandterminalsquote", ptQuote.Id, new ColumnSet(false));

                        application["lux_totaltechnicaltaxamount"] = new Money(TechnicalPremium * TechnicalTaxRate / 100);
                        application["lux_totalpolicytaxamount"] = new Money(PolicyPremium * PolicyTaxRate / 100);
                        application["lux_policytotaltax"] = PolicyTaxRate;

                        organizationService.Update(application);
                    }

                    var ptList = organizationService.RetrieveMultiple(new FetchExpression(FinalRatingfetch));
                    if (ptList.Entities.Count() > 0)
                    {
                        var TotalCommPercentage = ptList.Entities.Where(x => x.GetAttributeValue<OptionSetValue>("lux_percentageorflatfee").Value == 972970001).Sum(x => x.Attributes.Contains("lux_percentageamount") ? x.GetAttributeValue<decimal>("lux_percentageamount") : 0M);
                        var TotalFlatFee = ptList.Entities.Where(x => x.GetAttributeValue<OptionSetValue>("lux_percentageorflatfee").Value == 972970002).Sum(x => x.Attributes.Contains("lux_commissonamount") ? x.GetAttributeValue<Money>("lux_commissonamount").Value : 0M);

                        var TotalTechnicalBrokerCommAmt = TechnicalPremium * (TotalCommPercentage) / 100 + TotalFlatFee;
                        var TotalPolicyBrokerCommAmt = PolicyPremium * (TotalCommPercentage) / 100 + TotalFlatFee;


                        //var CommissionAmount = ptList.Entities.Sum(x => x.Attributes.Contains("lux_commissionamountforsubgrid") ? x.GetAttributeValue<Money>("lux_commissionamountforsubgrid").Value : 0M);
                        //var PolicyCommissionAmount = ptList.Entities.Sum(x => x.Attributes.Contains("lux_policycommissionamount") ? x.GetAttributeValue<Money>("lux_policycommissionamount").Value : 0M);

                        Entity application = organizationService.Retrieve("lux_portandterminalsquote", ptQuote.Id, new ColumnSet(false));

                        if (TechnicalPremium != 0)
                        {
                            application["lux_technicalbrokercommissionpercentage"] = TotalTechnicalBrokerCommAmt * 100 / TechnicalPremium;
                            application["lux_technicalbrokercommissionamount"] = new Money(TotalTechnicalBrokerCommAmt);
                        }

                        if (PolicyPremium != 0)
                        {
                            application["lux_policybrokercommissionpercentage"] = TotalPolicyBrokerCommAmt * 100 / PolicyPremium;
                            application["lux_policybrokercommissionamount"] = new Money(TotalPolicyBrokerCommAmt);
                        }

                        //application["lux_technicalbrokercommissionpercentage"] = CommissionAmount;
                        //application["lux_policybrokercommissionpercentage"] = PolicyCommissionAmount;

                        organizationService.Update(application);

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