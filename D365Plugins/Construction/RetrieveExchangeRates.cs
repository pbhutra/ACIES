using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using System;
using System.Linq;

namespace D365Plugins
{
    public class RetrieveExchangeRates : IPlugin
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

                    var TerrorismRow = organizationService.Retrieve("lux_constructionterrorism", entity.Id, new ColumnSet(true));
                    var constructionQuote = organizationService.Retrieve("lux_constructionquotes", TerrorismRow.GetAttributeValue<EntityReference>("lux_constructionquote").Id, new ColumnSet("lux_bipremiumexclipt", "lux_terrorismcommissionpercentage", "lux_iptrate"));

                    var exchangeRateList = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                                                  <entity name='lux_exchangerate'>
                                                    <attribute name='lux_isocurrencycode_from' />
                                                    <attribute name='lux_isocurrencycode_to' />
                                                    <attribute name='lux_exchangeratedate' />
                                                    <attribute name='lux_rate' />
                                                    <attribute name='lux_exchangerateid' />
                                                    <order attribute='lux_isocurrencycode_from' descending='false' />
                                                    <filter type='and'>
                                                      <condition attribute='statecode' operator='eq' value='0' />
                                                    </filter>
                                                  </entity>
                                                </fetch>";

                    var RateList = organizationService.RetrieveMultiple(new FetchExpression(exchangeRateList));
                    if (RateList.Entities.Count() > 0)
                    {

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