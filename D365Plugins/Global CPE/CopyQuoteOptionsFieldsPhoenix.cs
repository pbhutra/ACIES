using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using System;
using System.Linq;

namespace D365Plugins
{
    public class CopyQuoteOptionsFieldsPhoenix : IPlugin
    {
        public void Execute(IServiceProvider serviceProvider)
        {
            IPluginExecutionContext context = (IPluginExecutionContext)serviceProvider.GetService(typeof(IPluginExecutionContext));
            ITracingService tracingService = (ITracingService)serviceProvider.GetService(typeof(ITracingService));
            if (context.InputParameters.Contains("Target") && context.Depth == 1)
            {
                try
                {
                    // Obtain the organization service reference.
                    IOrganizationServiceFactory serviceFactory = (IOrganizationServiceFactory)serviceProvider.GetService(typeof(IOrganizationServiceFactory));
                    IOrganizationService organizationService = serviceFactory.CreateOrganizationService(context.UserId);

                    // Obtain the target entity from the input parameters.
                    Entity entity = new Entity();
                    entity = (Entity)context.InputParameters["Target"];

                    var cpePolicy = organizationService.Retrieve(entity.LogicalName, entity.Id, new ColumnSet(true));
                    var cpeQuote = organizationService.Retrieve("lux_contractorsplantandequipmentquote", cpePolicy.GetAttributeValue<EntityReference>("lux_contractorsplantandequipmentquote").Id, new ColumnSet("lux_wouldyouliketooffermultiplequoteoptions", "lux_quoteoptionselected", "lux_product"));

                    var quoteoption = cpeQuote.Attributes.Contains("lux_wouldyouliketooffermultiplequoteoptions") ? cpeQuote.FormattedValues["lux_wouldyouliketooffermultiplequoteoptions"] : "No";
                    var Product = cpeQuote.FormattedValues["lux_product"];

                    if (quoteoption == "Yes")
                    {
                        var selectedQuoteOption = organizationService.Retrieve("lux_phoenixquoteoption", cpeQuote.GetAttributeValue<EntityReference>("lux_quoteoptionselected").Id, new ColumnSet(true));

                        if (Product == "Contractors Plant and Equipment")
                        {
                            var tradefetch1 = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                                          <entity name='lux_phoenixquoteoptionlist'>
                                            <attribute name='lux_excess' />
                                            <attribute name='lux_limitofindemnity' />
                                            <attribute name='lux_rownumber' />
                                            <attribute name='lux_phoenixquoteoptionlistid' />
                                            <order attribute='lux_rownumber' descending='false' />
                                            <filter type='and'>
                                              <condition attribute='statecode' operator='eq' value='0' />
                                              <condition attribute='lux_phoenixquoteoption' operator='eq' uiname='' uitype='lux_phoenixquoteoption' value='{selectedQuoteOption.Id}' />
                                            </filter>
                                          </entity>
                                        </fetch>";

                            var tradeList1 = organizationService.RetrieveMultiple(new FetchExpression(tradefetch1)).Entities;

                            var OwnPlant = tradeList1.FirstOrDefault(x => x.GetAttributeValue<int>("lux_rownumber") == 1);
                            var HiredinPlant = tradeList1.FirstOrDefault(x => x.GetAttributeValue<int>("lux_rownumber") == 2);
                            var TemporaryBuildings = tradeList1.FirstOrDefault(x => x.GetAttributeValue<int>("lux_rownumber") == 3);
                            var Employeestools = tradeList1.FirstOrDefault(x => x.GetAttributeValue<int>("lux_rownumber") == 4);
                            var Otheritems = tradeList1.FirstOrDefault(x => x.GetAttributeValue<int>("lux_rownumber") == 5);
                            var Increasedcostofworking = tradeList1.FirstOrDefault(x => x.GetAttributeValue<int>("lux_rownumber") == 6);
                            var Terrorism = tradeList1.FirstOrDefault(x => x.GetAttributeValue<int>("lux_rownumber") == 13);

                            if (OwnPlant != null)
                            {
                                cpeQuote["lux_pleaseconfirmifcoverforownplantisrequired"] = true;
                                cpeQuote["lux_ownplantsingleitemlimit"] = OwnPlant.GetAttributeValue<decimal>("lux_limitofindemnity");
                            }
                            else
                            {
                                cpeQuote["lux_pleaseconfirmifcoverforownplantisrequired"] = false;
                                cpeQuote["lux_ownplantsingleitemlimit"] = new decimal(0);
                            }

                            if (HiredinPlant != null)
                            {
                                cpeQuote["lux_pleaseconfirmifcoverforhiredinplantisrequ"] = true;
                                cpeQuote["lux_hiredinplantanyoneitemlimit"] = HiredinPlant.GetAttributeValue<decimal>("lux_limitofindemnity");
                            }
                            else
                            {
                                cpeQuote["lux_pleaseconfirmifcoverforhiredinplantisrequ"] = false;
                                cpeQuote["lux_hiredinplantanyoneitemlimit"] = new decimal(0);
                            }

                            if (TemporaryBuildings != null)
                            {
                                cpeQuote["lux_pleaseconfirmifcoverfortemporarybuildings"] = true;
                                cpeQuote["lux_temporarybuildingsanyoneitemlimit"] = TemporaryBuildings.GetAttributeValue<decimal>("lux_limitofindemnity");
                            }
                            else
                            {
                                cpeQuote["lux_pleaseconfirmifcoverfortemporarybuildings"] = false;
                                cpeQuote["lux_temporarybuildingsanyoneitemlimit"] = new decimal(0);
                            }

                            if (Employeestools != null)
                            {
                                cpeQuote["lux_pleaseconfirmifcoverforemployeestoolsisre"] = true;
                                cpeQuote["lux_employeetoolsexcess"] = Employeestools.GetAttributeValue<decimal>("lux_excess");
                            }
                            else
                            {
                                cpeQuote["lux_pleaseconfirmifcoverforemployeestoolsisre"] = false;
                                cpeQuote["lux_employeetoolsexcess"] = new decimal(0);
                            }

                            if (Otheritems != null)
                            {
                                cpeQuote["lux_pleaseconfirmifcoverforotheritemsisrequir"] = true;
                                cpeQuote["lux_otheritemslimit"] = Otheritems.GetAttributeValue<decimal>("lux_limitofindemnity");
                                cpeQuote["lux_otheritemsexcess"] = Otheritems.GetAttributeValue<decimal>("lux_excess");
                            }
                            else
                            {
                                cpeQuote["lux_pleaseconfirmifcoverforotheritemsisrequir"] = false;
                                cpeQuote["lux_otheritemslimit"] = new decimal(0);
                                cpeQuote["lux_otheritemsexcess"] = new decimal(0);
                            }

                            if (Increasedcostofworking != null)
                            {
                                cpeQuote["lux_pleaseconfirmifcoverforincreasedcostofwor"] = true;
                                cpeQuote["lux_increasedcostofworkinglimitofindemnity"] = Increasedcostofworking.GetAttributeValue<decimal>("lux_limitofindemnity");
                            }
                            else
                            {
                                cpeQuote["lux_pleaseconfirmifcoverforincreasedcostofwor"] = false;
                                cpeQuote["lux_increasedcostofworkinglimitofindemnity"] = new decimal(0);
                            }

                            if (Terrorism != null)
                            {
                                cpeQuote["lux_iscoverrequiredforterrorism"] = true;
                            }
                            else
                            {
                                cpeQuote["lux_iscoverrequiredforterrorism"] = false;
                            }

                            organizationService.Update(cpeQuote);
                        }
                        else if (Product == "Contractors All Risk")
                        {

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