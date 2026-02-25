using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using System;
using System.Linq;

namespace D365Plugins
{
    public class CalculateTotalIPTPortTerminals : IPlugin
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

                    var PremiumRow = organizationService.Retrieve("lux_portandterminalquotetaxtype", entity.Id, new ColumnSet(true));
                    var ptQuote = organizationService.Retrieve("lux_portandterminalsquote", PremiumRow.GetAttributeValue<EntityReference>("lux_portandterminalsquote").Id, new ColumnSet("lux_technicalpremiumbeforetax", "lux_policypremiumbeforetax"));
                    var TechnicalPremium = ptQuote.Attributes.Contains("lux_technicalpremiumbeforetax") ? ptQuote.GetAttributeValue<Money>("lux_technicalpremiumbeforetax").Value : 0;
                    var PolicyPremium = ptQuote.Attributes.Contains("lux_policypremiumbeforetax") ? ptQuote.GetAttributeValue<Money>("lux_policypremiumbeforetax").Value : TechnicalPremium;

                    var FinalRatingfetch = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
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

                    if (context.MessageName == "Delete")
                    {
                        FinalRatingfetch = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                                                  <entity name='lux_portandterminalquotetaxtype'>
                                                    <attribute name='lux_name' />
                                                    <attribute name='lux_taxpercentage' />
                                                    <attribute name='lux_portandterminalquotetaxtypeid' />
                                                    <filter type='and'>
                                                      <condition attribute='statecode' operator='eq' value='0' />
                                                      <condition attribute='lux_portandterminalsquote' operator='eq' uiname='' uitype='lux_portandterminalsquote' value='{ptQuote.Id}' />
                                                      <condition attribute='lux_portandterminalquotetaxtypeid' operator='ne' uiname='' uitype='lux_portandterminalquotetaxtype' value='{entity.Id}' />
                                                    </filter>
                                                  </entity>
                                                </fetch>";
                    }

                    var ptList = organizationService.RetrieveMultiple(new FetchExpression(FinalRatingfetch));
                    if (ptList.Entities.Count() > 0)
                    {
                        var TechnicalTaxRate = ptList.Entities.Sum(x => x.Attributes.Contains("lux_taxpercentage") ? x.GetAttributeValue<decimal>("lux_taxpercentage") : 0);
                        var PolicyTaxRate = ptList.Entities.Sum(x => x.Attributes.Contains("lux_taxpercentage") ? x.GetAttributeValue<decimal>("lux_taxpercentage") : 0);

                        Entity application = organizationService.Retrieve("lux_portandterminalsquote", ptQuote.Id, new ColumnSet(false));
                        application["lux_totaltechnicaltaxamount"] = new Money(TechnicalPremium * TechnicalTaxRate / 100);
                        application["lux_totalpolicytaxamount"] = new Money(PolicyPremium * PolicyTaxRate / 100);
                        application["lux_policytotaltax"] = PolicyTaxRate;
                        organizationService.Update(application);
                    }
                    else
                    {
                        Entity application = organizationService.Retrieve("lux_portandterminalsquote", ptQuote.Id, new ColumnSet(false));
                        application["lux_totaltechnicaltaxamount"] = new Money(0);
                        application["lux_totalpolicytaxamount"] = new Money(0);
                        application["lux_policytotaltax"] = 0M;
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