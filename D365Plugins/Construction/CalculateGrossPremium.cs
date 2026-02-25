using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using System;
using System.Linq;

namespace D365Plugins
{
    public class CalculateGrossPremium : IPlugin
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

                    var PremiumRow = organizationService.Retrieve("lux_constructiontechnicalpremiumcalculation", entity.Id, new ColumnSet(true));
                    var constructionQuote = organizationService.Retrieve("lux_constructionquotes", PremiumRow.GetAttributeValue<EntityReference>("lux_constructionquote").Id, new ColumnSet("lux_brokercommission", "lux_pslcommission", "lux_typeofpolicy1", "lux_phoenixsharebound"));
                    var BoundShare = constructionQuote.Attributes.Contains("lux_phoenixsharebound") ? constructionQuote.GetAttributeValue<decimal>("lux_phoenixsharebound") : 0;
                    var QuotedPremium = PremiumRow.Attributes.Contains("lux_grosspolicypremiumquoted") ? PremiumRow.GetAttributeValue<Money>("lux_grosspolicypremiumquoted").Value : 0;

                    PremiumRow["lux_grosspolicypremiumbound"] = QuotedPremium * BoundShare / 100;
                    organizationService.Update(PremiumRow);

                    var FinalRatingfetch = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                              <entity name='lux_constructiontechnicalpremiumcalculation'>
                                <attribute name='lux_name' />
                                <attribute name='lux_ratingbasis' />
                                <attribute name='lux_rate' />
                                <attribute name='lux_constructionquote' />
                                <attribute name='lux_grosspolicypremiumquoted' /> 
                                <attribute name='lux_grosspolicypremiumbound' /> 
                                <attribute name='lux_nettechnicalpremium' />
                                <attribute name='lux_ratingfigures' />
                                <attribute name='lux_quoteratedeviation' />
                                <attribute name='lux_boundratedeviation' />
                                <attribute name='lux_technicalpremiumcalculation' />
                                <attribute name='transactioncurrencyid' />
                                <attribute name='lux_constructiontechnicalpremiumcalculationid' />
                                <order attribute='lux_name' descending='false' />
                                <filter type='and'>
                                  <condition attribute='statecode' operator='eq' value='0' />
                                  <condition attribute='lux_constructionquote' operator='eq' uiname='' uitype='lux_constructionquotes' value='{constructionQuote.Id}' />
                                </filter>
                              </entity>
                            </fetch>";

                    var ConstructionList = organizationService.RetrieveMultiple(new FetchExpression(FinalRatingfetch));
                    if (ConstructionList.Entities.Count() > 0)
                    {
                        var NetPremium = ConstructionList.Entities.Sum(x => x.Attributes.Contains("lux_nettechnicalpremium") ? x.GetAttributeValue<Money>("lux_nettechnicalpremium").Value : 0);
                        var GrossPremiumQuoted = ConstructionList.Entities.Sum(x => x.Attributes.Contains("lux_grosspolicypremiumquoted") ? x.GetAttributeValue<Money>("lux_grosspolicypremiumquoted").Value : 0);
                        var GrossPremiumBound = ConstructionList.Entities.Sum(x => x.Attributes.Contains("lux_grosspolicypremiumbound") ? x.GetAttributeValue<Money>("lux_grosspolicypremiumbound").Value : 0);

                        var QuoteRateDeviation = (GrossPremiumQuoted / NetPremium) - 1;
                        var BoundRateDeviation = (GrossPremiumBound / NetPremium) - 1;

                        Entity application = organizationService.Retrieve("lux_constructionquotes", constructionQuote.Id, new ColumnSet(false));

                        if (constructionQuote.FormattedValues["lux_typeofpolicy1"] == "Single Project")
                        {
                            application["lux_constructioncombinedprojectsection"] = ConstructionList.Entities.FirstOrDefault(x => x.GetAttributeValue<OptionSetValue>("lux_technicalpremiumcalculation").Value == 972970001) != null ? ConstructionList.Entities.FirstOrDefault(x => x.GetAttributeValue<OptionSetValue>("lux_technicalpremiumcalculation").Value == 972970001).Attributes.Contains("lux_grosspolicypremiumbound") ? ConstructionList.Entities.FirstOrDefault(x => x.GetAttributeValue<OptionSetValue>("lux_technicalpremiumcalculation").Value == 972970001).GetAttributeValue<Money>("lux_grosspolicypremiumbound") : new Money(0) : new Money(0);
                        }
                        else
                        {
                            application["lux_constructioncombinedprojectsection"] = ConstructionList.Entities.FirstOrDefault(x => x.GetAttributeValue<OptionSetValue>("lux_technicalpremiumcalculation").Value == 972970002) != null ? ConstructionList.Entities.FirstOrDefault(x => x.GetAttributeValue<OptionSetValue>("lux_technicalpremiumcalculation").Value == 972970002).Attributes.Contains("lux_grosspolicypremiumbound") ? ConstructionList.Entities.FirstOrDefault(x => x.GetAttributeValue<OptionSetValue>("lux_technicalpremiumcalculation").Value == 972970002).GetAttributeValue<Money>("lux_grosspolicypremiumbound") : new Money(0) : new Money(0);
                        }

                        application["lux_delayinstartupsection"] = ConstructionList.Entities.FirstOrDefault(x => x.GetAttributeValue<OptionSetValue>("lux_technicalpremiumcalculation").Value == 972970009) != null ? ConstructionList.Entities.FirstOrDefault(x => x.GetAttributeValue<OptionSetValue>("lux_technicalpremiumcalculation").Value == 972970009).Attributes.Contains("lux_grosspolicypremiumbound") ? ConstructionList.Entities.FirstOrDefault(x => x.GetAttributeValue<OptionSetValue>("lux_technicalpremiumcalculation").Value == 972970009).GetAttributeValue<Money>("lux_grosspolicypremiumbound") : new Money(0) : new Money(0);
                        application["lux_existingstructurespremium"] = ConstructionList.Entities.FirstOrDefault(x => x.GetAttributeValue<OptionSetValue>("lux_technicalpremiumcalculation").Value == 972970003) != null ? ConstructionList.Entities.FirstOrDefault(x => x.GetAttributeValue<OptionSetValue>("lux_technicalpremiumcalculation").Value == 972970003).Attributes.Contains("lux_grosspolicypremiumbound") ? ConstructionList.Entities.FirstOrDefault(x => x.GetAttributeValue<OptionSetValue>("lux_technicalpremiumcalculation").Value == 972970003).GetAttributeValue<Money>("lux_grosspolicypremiumbound") : new Money(0) : new Money(0);
                        application["lux_publicliabilitybuildingandalliedtradessec"] = ConstructionList.Entities.FirstOrDefault(x => x.GetAttributeValue<OptionSetValue>("lux_technicalpremiumcalculation").Value == 972970010) != null ? ConstructionList.Entities.FirstOrDefault(x => x.GetAttributeValue<OptionSetValue>("lux_technicalpremiumcalculation").Value == 972970010).Attributes.Contains("lux_grosspolicypremiumbound") ? ConstructionList.Entities.FirstOrDefault(x => x.GetAttributeValue<OptionSetValue>("lux_technicalpremiumcalculation").Value == 972970010).GetAttributeValue<Money>("lux_grosspolicypremiumbound") : new Money(0) : new Money(0);
                        application["lux_nonnegligentdamagesection"] = ConstructionList.Entities.FirstOrDefault(x => x.GetAttributeValue<OptionSetValue>("lux_technicalpremiumcalculation").Value == 972970011) != null ? ConstructionList.Entities.FirstOrDefault(x => x.GetAttributeValue<OptionSetValue>("lux_technicalpremiumcalculation").Value == 972970011).Attributes.Contains("lux_grosspolicypremiumbound") ? ConstructionList.Entities.FirstOrDefault(x => x.GetAttributeValue<OptionSetValue>("lux_technicalpremiumcalculation").Value == 972970011).GetAttributeValue<Money>("lux_grosspolicypremiumbound") : new Money(0) : new Money(0);
                        application["lux_contractorsplantandequipmentsection"] = ConstructionList.Entities.FirstOrDefault(x => x.GetAttributeValue<OptionSetValue>("lux_technicalpremiumcalculation").Value == 972970004) != null ? ConstructionList.Entities.FirstOrDefault(x => x.GetAttributeValue<OptionSetValue>("lux_technicalpremiumcalculation").Value == 972970004).Attributes.Contains("lux_grosspolicypremiumbound") ? ConstructionList.Entities.FirstOrDefault(x => x.GetAttributeValue<OptionSetValue>("lux_technicalpremiumcalculation").Value == 972970004).GetAttributeValue<Money>("lux_grosspolicypremiumbound") : new Money(0) : new Money(0);
                        application["lux_employeetoolssection"] = ConstructionList.Entities.FirstOrDefault(x => x.GetAttributeValue<OptionSetValue>("lux_technicalpremiumcalculation").Value == 972970006) != null ? ConstructionList.Entities.FirstOrDefault(x => x.GetAttributeValue<OptionSetValue>("lux_technicalpremiumcalculation").Value == 972970006).Attributes.Contains("lux_grosspolicypremiumbound") ? ConstructionList.Entities.FirstOrDefault(x => x.GetAttributeValue<OptionSetValue>("lux_technicalpremiumcalculation").Value == 972970006).GetAttributeValue<Money>("lux_grosspolicypremiumbound") : new Money(0) : new Money(0);

                        application["lux_ecv"] = new Money(GrossPremiumBound);
                        application["lux_nettechnicalpremium"] = new Money(NetPremium);
                        application["lux_grosspolicypremiumbound"] = new Money(GrossPremiumBound);

                        if (constructionQuote.Attributes.Contains("lux_brokercommission"))
                            application["lux_brokercommissionamount"] = GrossPremiumQuoted * constructionQuote.GetAttributeValue<decimal>("lux_brokercommission") / 100;

                        if (constructionQuote.Attributes.Contains("lux_pslcommission"))
                            application["lux_pslcommissionamount"] = GrossPremiumQuoted * constructionQuote.GetAttributeValue<decimal>("lux_pslcommission") / 100;

                        application["lux_quoteratedeviation"] = QuoteRateDeviation * 100;
                        application["lux_boundratedeviation"] = BoundRateDeviation * 100;

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