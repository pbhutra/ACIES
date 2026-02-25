using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using System;
using System.Linq;

namespace D365Plugins
{
    public class CalculateGrossPremiumSubscribe : IPlugin
    {
        public void Execute(IServiceProvider serviceProvider)
        {
            IPluginExecutionContext context = (IPluginExecutionContext)serviceProvider.GetService(typeof(IPluginExecutionContext));
            ITracingService tracingService = (ITracingService)serviceProvider.GetService(typeof(ITracingService));

            if (context.InputParameters.Contains("Target") && context.InputParameters["Target"] is Entity && context.Depth <= 2)
            {
                // Obtain the target entity from the input parameters.
                Entity entity = (Entity)context.InputParameters["Target"];
                try
                {
                    // Obtain the organization service reference.
                    IOrganizationServiceFactory serviceFactory = (IOrganizationServiceFactory)serviceProvider.GetService(typeof(IOrganizationServiceFactory));
                    IOrganizationService organizationService = serviceFactory.CreateOrganizationService(context.UserId);

                    var PremiumRow = organizationService.Retrieve("lux_subscribepisectionpremium", entity.Id, new ColumnSet(true));
                    var subsQuote = organizationService.Retrieve("lux_subscribequoteoption", PremiumRow.GetAttributeValue<EntityReference>("lux_subscribequoteoption").Id, new ColumnSet(true));

                    var FinalRatingfetch = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                                              <entity name='lux_subscribepisectionpremium'>
                                                <attribute name='lux_name' />
                                                <attribute name='lux_section' />
                                                <attribute name='lux_technicalpremium' />
                                                <attribute name='lux_policypremium' />
                                                <attribute name='transactioncurrencyid' />
                                                <attribute name='lux_subscribepisectionpremiumid' />
                                                <order attribute='lux_name' descending='false' />
                                                <filter type='and'>
                                                  <condition attribute='statecode' operator='eq' value='0' />
                                                  <condition attribute='lux_subscribequoteoption' operator='eq' uiname='' uitype='lux_subscribequoteoption' value='{subsQuote.Id}' />
                                                </filter>
                                              </entity>
                                            </fetch>";

                    var premiumList = organizationService.RetrieveMultiple(new FetchExpression(FinalRatingfetch));
                    if (premiumList.Entities.Count() > 0)
                    {
                        var TechnicalPremium = premiumList.Entities.Sum(x => x.Attributes.Contains("lux_technicalpremium") ? x.GetAttributeValue<Money>("lux_technicalpremium").Value : 0);
                        var PolicyPremium = premiumList.Entities.Sum(x => x.Attributes.Contains("lux_policypremium") ? x.GetAttributeValue<Money>("lux_policypremium").Value : 0);

                        Entity application = organizationService.Retrieve("lux_subscribequoteoption", subsQuote.Id, new ColumnSet(false));

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