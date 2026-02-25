using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using System;
using System.Linq;

namespace D365Plugins
{
    public class CalculateTerrorismPremium : IPlugin
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

                    var TerrorismListFetch = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                                                  <entity name='lux_constructionterrorism'>
                                                    <attribute name='lux_zonepremiumexclipt' />
                                                    <attribute name='lux_previouszonesuminsured' />
                                                    <attribute name='lux_poolrezone' />
                                                    <attribute name='lux_newzonesuminsured' />
                                                    <attribute name='lux_firstpartofpostcodeofsiteaddress' />
                                                    <attribute name='lux_constructionterrorismid' />
                                                    <order attribute='lux_firstpartofpostcodeofsiteaddress' descending='false' />
                                                    <filter type='and'>
                                                      <condition attribute='statecode' operator='eq' value='0' />
                                                      <condition attribute='lux_constructionquote' operator='eq' uiname='' uitype='lux_constructionquotes' value='{constructionQuote.Id}' />
                                                    </filter>
                                                  </entity>
                                                </fetch>";

                    var ConstructionList = organizationService.RetrieveMultiple(new FetchExpression(TerrorismListFetch));
                    if (ConstructionList.Entities.Count() > 0)
                    {
                        var ZonePRemium = ConstructionList.Entities.Sum(x => x.Contains("lux_zonepremiumexclipt") == true ? x.GetAttributeValue<Money>("lux_zonepremiumexclipt").Value : 0);
                        var BIPRemium = constructionQuote.Attributes.Contains("lux_bipremiumexclipt") ? constructionQuote.GetAttributeValue<Money>("lux_bipremiumexclipt").Value : 0;
                        var TerrBrokerComm = constructionQuote.Attributes.Contains("lux_terrorismcommissionpercentage") ? constructionQuote.GetAttributeValue<decimal>("lux_terrorismcommissionpercentage") : 0;
                        var IPTRate = constructionQuote.Attributes.Contains("lux_iptrate") ? constructionQuote.GetAttributeValue<decimal>("lux_iptrate") : 12M;

                        var TotalPremium = ZonePRemium + BIPRemium;
                        constructionQuote["lux_terrorismsection"] = new Money(TotalPremium);
                        constructionQuote["lux_terrorismpremiumipt"] = new Money(TotalPremium * IPTRate / 100);
                        constructionQuote["lux_thebrokercommission"] = new Money(TotalPremium * TerrBrokerComm / 100);

                        organizationService.Update(constructionQuote);
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