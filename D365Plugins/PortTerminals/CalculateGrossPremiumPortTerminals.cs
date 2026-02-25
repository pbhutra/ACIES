using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using System;
using System.Linq;

namespace D365Plugins
{
    public class CalculateGrossPremiumPortTerminals : IPlugin
    {
        public void Execute(IServiceProvider serviceProvider)
        {
            IPluginExecutionContext context = (IPluginExecutionContext)serviceProvider.GetService(typeof(IPluginExecutionContext));
            ITracingService tracingService = (ITracingService)serviceProvider.GetService(typeof(ITracingService));

            if (context.InputParameters.Contains("Target") && context.InputParameters["Target"] is Entity && context.Depth == 1)
            {
                // Obtain the target entity from the input parameters.
                Entity entity = (Entity)context.InputParameters["Target"];
                try
                {
                    // Obtain the organization service reference.
                    IOrganizationServiceFactory serviceFactory = (IOrganizationServiceFactory)serviceProvider.GetService(typeof(IOrganizationServiceFactory));
                    IOrganizationService organizationService = serviceFactory.CreateOrganizationService(context.UserId);

                    var PremiumRow = organizationService.Retrieve("lux_portandterminalsquotepremium", entity.Id, new ColumnSet(true));
                    var CPEQuote = organizationService.Retrieve("lux_portandterminalsquote", PremiumRow.GetAttributeValue<EntityReference>("lux_portandterminalsquote").Id, new ColumnSet(true));

                    //var TechnicalBrokerCommission = CPEQuote.Attributes.Contains("lux_technicalbrokercommissionpercentage") ? CPEQuote.GetAttributeValue<decimal>("lux_technicalbrokercommissionpercentage") : 16.75M;
                    //var TechnicalACIESCommission = CPEQuote.Attributes.Contains("lux_technicalmgacommissionpercentage") ? CPEQuote.GetAttributeValue<decimal>("lux_technicalmgacommissionpercentage") : 10;

                    //var TotalTechnicalCommission = TechnicalBrokerCommission + TechnicalACIESCommission;

                    //var PolicyBrokerCommission = CPEQuote.Attributes.Contains("lux_policybrokercommissionpercentage") ? CPEQuote.GetAttributeValue<decimal>("lux_policybrokercommissionpercentage") : 16.75M;
                    //var PolicyACIESCommission = CPEQuote.Attributes.Contains("lux_policymgacommissionpercentage") ? CPEQuote.GetAttributeValue<decimal>("lux_policymgacommissionpercentage") : 10;

                    //var TotalPolicyCommission = PolicyBrokerCommission + PolicyACIESCommission;

                    //throw new InvalidPluginExecutionException(recruitmentQuote.Id.ToString());

                    var FinalRatingfetch = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                                              <entity name='lux_portandterminalsquotepremium'>
                                                <attribute name='lux_name' />
                                                <attribute name='lux_section' />
                                                <attribute name='lux_portandterminalsquote' />
                                                <attribute name='lux_technicalpremium' />
                                                <attribute name='lux_policypremium' />
                                                <attribute name='transactioncurrencyid' />
                                                <attribute name='lux_portandterminalsquotepremiumid' />
                                                <order attribute='lux_name' descending='false' />
                                                <filter type='and'>
                                                  <condition attribute='statecode' operator='eq' value='0' />
                                                  <condition attribute='lux_portandterminalsquote' operator='eq' uiname='' uitype='lux_portandterminalsquote' value='{CPEQuote.Id}' />
                                                </filter>
                                              </entity>
                                            </fetch>";

                    var recruitmentList = organizationService.RetrieveMultiple(new FetchExpression(FinalRatingfetch));
                    if (recruitmentList.Entities.Count() > 0)
                    {
                        var TechnicalPremium = recruitmentList.Entities.Sum(x => x.Attributes.Contains("lux_technicalpremium") ? x.GetAttributeValue<Money>("lux_technicalpremium").Value : 0);
                        var PolicyPremium = recruitmentList.Entities.Sum(x => x.Attributes.Contains("lux_policypremium") ? x.GetAttributeValue<Money>("lux_policypremium").Value : 0);


                        Entity application = organizationService.Retrieve("lux_portandterminalsquote", CPEQuote.Id, new ColumnSet(false));

                        application["lux_technicalpremiumbeforetax"] = new Money(TechnicalPremium);
                        application["lux_policypremiumbeforetax"] = new Money(PolicyPremium);

                        organizationService.Update(application);
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