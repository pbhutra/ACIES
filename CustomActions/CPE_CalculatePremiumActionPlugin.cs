using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Acies_Customization.CustomActions
{
    public class CPE_CalculatePremiumActionPlugin : IPlugin
    {
        public void Execute(IServiceProvider serviceProvider)
        {
            IPluginExecutionContext context = (IPluginExecutionContext)serviceProvider.GetService(typeof(IPluginExecutionContext));
            ITracingService tracingService = (ITracingService)serviceProvider.GetService(typeof(ITracingService));
            tracingService.Trace("CPE_CalculatePremiumActionPlugin execution started.");


            if (context.InputParameters.Contains("Target") && context.Depth == 1)
            {
                // Obtain the target entity from the input parameters.
                Entity entity = new Entity();
                try
                {
                    // Obtain the organization service reference.
                    IOrganizationServiceFactory serviceFactory = (IOrganizationServiceFactory)serviceProvider.GetService(typeof(IOrganizationServiceFactory));
                    IOrganizationService organizationService = serviceFactory.CreateOrganizationService(context.UserId);

                    EntityReference e = (EntityReference)context.InputParameters["Target"];
                    entity = organizationService.Retrieve(e.LogicalName, e.Id, new ColumnSet(true));

                    var phoenixQuote = organizationService.Retrieve(entity.LogicalName, entity.Id, new ColumnSet(true));

                    var cpeQuote = organizationService.Retrieve(entity.LogicalName, entity.Id, new ColumnSet(false));
                    cpeQuote["lux_policypremiumbeforetax"] = new Money(0);
                    cpeQuote["lux_policybrokercommissionamount"] = new Money(0);
                    cpeQuote["lux_policybrokercommissionpercentage"] = null;
                    cpeQuote["lux_policymgacommissionpercentage"] = null;
                    cpeQuote["lux_policyaciesmgucommissionpercentage"] = null;
                    cpeQuote["lux_policytotaltaxamount"] = new Money(0);
                    cpeQuote["lux_policytotaltax"] = null;
                    cpeQuote["lux_policypolicyfee"] = new Money(0);
                    cpeQuote["lux_totallineamount"] = new Money(0);
                    organizationService.Update(cpeQuote);

                    var Product = phoenixQuote.FormattedValues["lux_product"];

                    if (Product == "Contractors Plant and Equipment" || Product == "Contractors All Risk")
                    {
                        var quoteOptionfetch = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                                                  <entity name='lux_phoenixquoteoption'>
                                                    <attribute name='lux_phoenixquoteoptionid' />
                                                    <attribute name='lux_name' />
                                                    <attribute name='createdon' />
                                                    <order attribute='lux_name' descending='false' />
                                                    <filter type='and'>
                                                      <condition attribute='statecode' operator='eq' value='0' />
                                                      <condition attribute='lux_contractorsplantandequipmentquote' operator='eq' uiname='' uitype='lux_contractorsplantandequipmentquote' value='{phoenixQuote.Id}' />
                                                    </filter>
                                                  </entity>
                                                </fetch>";

                        var quoteOptionsList = organizationService.RetrieveMultiple(new FetchExpression(quoteOptionfetch));

                        String[][] array3d = new String[quoteOptionsList.Entities.Count][];
                        var j = 0;

                        if (quoteOptionsList.Entities.Count > 1)
                        {
                            foreach (var item in quoteOptionsList.Entities)
                            {
                                var optionlistFetch = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                                              <entity name='lux_phoenixquoteoptionlist'>
                                                <attribute name='createdon' />
                                                <attribute name='lux_limitofindemnity' />
                                                <attribute name='lux_excess' />
                                                <attribute name='lux_cover' />
                                                <attribute name='lux_phoenixquoteoptionlistid' />
                                                <attribute name='lux_phoenixquoteoption' />
                                                <order attribute='lux_rownumber' descending='false' />
                                                <filter type='and'>
                                                  <condition attribute='statecode' operator='eq' value='0' />
                                                  <condition attribute='lux_phoenixquoteoption' operator='eq' uiname='' uitype='lux_phoenixquoteoption' value='{item.Id}' />
                                                </filter>
                                              </entity>
                                            </fetch>";

                                var optionlistList = organizationService.RetrieveMultiple(new FetchExpression(optionlistFetch));
                                String[] array3d1 = new String[optionlistList.Entities.Count];
                                var i = 0;
                                foreach (var listitem in optionlistList.Entities)
                                {
                                    array3d1.SetValue(listitem.GetAttributeValue<OptionSetValue>("lux_cover").Value + "," + listitem.GetAttributeValue<decimal>("lux_limitofindemnity").ToString("#.00") + "," + listitem.GetAttributeValue<decimal>("lux_excess").ToString("#.00"), i);
                                    i++;
                                }

                                for (int k = 0; k < array3d.Distinct().Count() - 1; k++)
                                {
                                    if (array3d[k].SequenceEqual(array3d1))
                                    {
                                        throw new InvalidPluginExecutionException("You can not add cover type of the same Limit and same Excess");
                                    }
                                }
                                array3d.SetValue(array3d1, j);
                                j++;
                            }
                        }

                        var quoteoption = phoenixQuote.Attributes.Contains("lux_wouldyouliketooffermultiplequoteoptions") ? phoenixQuote.FormattedValues["lux_wouldyouliketooffermultiplequoteoptions"] : "No";
                        var OptionCount = phoenixQuote.Contains("lux_quoteoptionscount") ? phoenixQuote.GetAttributeValue<int>("lux_quoteoptionscount") : 0;

                        var tradefetch1 = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                                          <entity name='lux_contractorsplantandequipmentquotepremui'>
                                            <attribute name='lux_sectionreference' />
                                            <attribute name='lux_technicalpremium' />
                                            <attribute name='lux_ratingfigures' />
                                            <attribute name='lux_ratedeviation' />
                                            <attribute name='lux_policypremium' />
                                            <attribute name='lux_justificaiton' />
                                            <attribute name='lux_loaddiscount' />
                                            <attribute name='lux_comment' />
                                            <attribute name='lux_section' />
                                            <attribute name='lux_contractorsplantandequipmentquotepremuiid' />
                                            <order attribute='lux_sectionreference' descending='false' />
                                            <filter type='and'>
                                              <condition attribute='statecode' operator='eq' value='0' />
                                            </filter>
                                            <link-entity name='lux_phoenixquoteoption' from='lux_phoenixquoteoptionid' to='lux_phoenixquoteoption' link-type='inner' alias='ab'>
                                              <filter type='and'>
                                                <condition attribute='lux_contractorsplantandequipmentquote' operator='eq' uiname='' uitype='lux_contractorsplantandequipmentquote' value='{phoenixQuote.Id}' />
                                              </filter>
                                            </link-entity>
                                          </entity>
                                        </fetch>";

                        var tradeList1 = organizationService.RetrieveMultiple(new FetchExpression(tradefetch1));
                        foreach (var item in tradeList1.Entities)
                        {
                            organizationService.Delete("lux_contractorsplantandequipmentquotepremui", item.Id);
                        }

                        if (phoenixQuote.Attributes.Contains("lux_quoteoption1"))
                        {
                            var tradefetch = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                                              <entity name='lux_phoenixquoteoptionlist'>
                                                <attribute name='createdon' />
                                                <attribute name='lux_limitofindemnity' />
                                                <attribute name='lux_excess' />
                                                <attribute name='lux_cover' />
                                                <attribute name='lux_phoenixquoteoptionlistid' />
                                                <attribute name='lux_phoenixquoteoption' />
                                                <order attribute='createdon' descending='false' />
                                                <filter type='and'>
                                                  <condition attribute='statecode' operator='eq' value='0' />
                                                </filter>
                                                <link-entity name='lux_phoenixquoteoption' from='lux_phoenixquoteoptionid' to='lux_phoenixquoteoption' link-type='inner' alias='ab'>
                                                  <filter type='and'>
                                                    <condition attribute='lux_contractorsplantandequipmentquote' operator='eq' uiname='' uitype='lux_contractorsplantandequipmentquote' value='{phoenixQuote.Id}' />
                                                  </filter>
                                                </link-entity>
                                              </entity>
                                            </fetch>";

                            var tradeList = organizationService.RetrieveMultiple(new FetchExpression(tradefetch));
                            //throw new InvalidPluginExecutionException(tradeList.Entities.Count.ToString());
                            if (tradeList.Entities.Count() > 0)
                            {
                                foreach (var item1 in tradeList.Entities.GroupBy(x => x.GetAttributeValue<EntityReference>("lux_phoenixquoteoption")))
                                {
                                    var limit = 0M;
                                    foreach (var item in item1)
                                    {
                                        var tradeCover = item.Contains("lux_cover") ? item.GetAttributeValue<OptionSetValue>("lux_cover").Value : 0;
                                        var tradeExcess = item.Contains("lux_excess") ? item.GetAttributeValue<decimal>("lux_excess") : 0M;
                                        var tradeLimit = item.Contains("lux_limitofindemnity") ? item.GetAttributeValue<decimal>("lux_limitofindemnity") : 0M;

                                        Entity phoenixsectionpremium = new Entity("lux_contractorsplantandequipmentquotepremui");
                                        phoenixsectionpremium["lux_section"] = new OptionSetValue(tradeCover);
                                        if (tradeCover == 972970006)
                                        {
                                            phoenixsectionpremium["lux_sectionreference"] = "6T";
                                        }
                                        else
                                        {
                                            phoenixsectionpremium["lux_sectionreference"] = "CB";
                                        }
                                        limit += tradeLimit;
                                        phoenixsectionpremium["lux_ratingfigures"] = new Money(tradeLimit);
                                        phoenixsectionpremium["transactioncurrencyid"] = new EntityReference("transactioncurrency", phoenixQuote.GetAttributeValue<EntityReference>("transactioncurrencyid").Id);
                                        phoenixsectionpremium["lux_phoenixquoteoption"] = new EntityReference("lux_phoenixquoteoption", item.GetAttributeValue<EntityReference>("lux_phoenixquoteoption").Id);
                                        phoenixsectionpremium["lux_contractorsplantandequipmentquote"] = new EntityReference("lux_contractorsplantandequipmentquote", phoenixQuote.Id);
                                        organizationService.Create(phoenixsectionpremium);
                                    }

                                    Entity phoenixsectionpremium1 = new Entity("lux_contractorsplantandequipmentquotepremui");
                                    phoenixsectionpremium1["lux_section"] = new OptionSetValue(972970013);
                                    phoenixsectionpremium1["lux_ratingfigures"] = new Money(limit);
                                    phoenixsectionpremium1["transactioncurrencyid"] = new EntityReference("transactioncurrency", phoenixQuote.GetAttributeValue<EntityReference>("transactioncurrencyid").Id);
                                    phoenixsectionpremium1["lux_phoenixquoteoption"] = new EntityReference("lux_phoenixquoteoption", item1.FirstOrDefault().GetAttributeValue<EntityReference>("lux_phoenixquoteoption").Id);
                                    phoenixsectionpremium1["lux_contractorsplantandequipmentquote"] = new EntityReference("lux_contractorsplantandequipmentquote", phoenixQuote.Id);
                                    organizationService.Create(phoenixsectionpremium1);
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






            //try
            //{
            //    //foreach (var key in context.InputParameters.Keys)
            //    //{
            //    //    var val = context.InputParameters[key];
            //    //    tracingService.Trace($"Input key: {key}, value: {val}, type: {val?.GetType().FullName}");
            //    //}

            //    if (!(context.InputParameters.TryGetValue("Target", out var targetObj) && targetObj is EntityReference cpeQuoteRef))
            //    {
            //        throw new InvalidPluginExecutionException("CPE record could not be identified.");
            //    }

            //    var columns = new ColumnSet(
            //        "lux_brokercompanyname",
            //        "lux_product",
            //        "lux_inceptiondate",
            //        "lux_pleaseconfirmifcoverforownplantisrequired",
            //        "lux_totalownplantvalue",
            //        "lux_pleaseconfirmifcoverforhiredinplantisrequ",
            //        "lux_totalhiredplantvalue",
            //        "lux_pleaseconfirmifcoverfortemporarybuildings",
            //        "lux_temporarybuildingssuminsured",
            //        "lux_pleaseconfirmifcoverforemployeestoolsisre",
            //        "lux_employeestoolstotalvalue",
            //        "lux_pleaseconfirmifcoverforotheritemsisrequir",
            //        "lux_otheritemslimit",
            //        "lux_pleaseconfirmifcoverforincreasedcostofwor",
            //        "lux_increasedcostofworkinglimitofindemnity",
            //        "lux_iscoverrequiredforterrorism",
            //        "lux_acentralbusinessdistrict",
            //        "lux_bmetropolitan",
            //        "lux_crural",
            //        "lux_technicalbrokercommissionpercentage",
            //        "lux_technicalaciesmgucommissionpercentage",
            //        "lux_policybrokercommissionpercentage",
            //        "lux_policyaciesmgucommissionpercentage",
            //        "lux_applicationtype",
            //        "transactioncurrencyid"
            //    );

            //    var cpeEntity = service.Retrieve(cpeQuoteRef.LogicalName, cpeQuoteRef.Id, columns);

            //    var inceptionDate = Convert.ToDateTime(cpeEntity.FormattedValues["lux_inceptiondate"], System.Globalization.CultureInfo.GetCultureInfo("en-GB").DateTimeFormat);

            //    EntityReference premiumCurrency = cpeEntity.GetAttributeValue<EntityReference>("transactioncurrencyid");

            //    bool ownPlantCover = cpeEntity.GetAttributeValue<bool?>("lux_pleaseconfirmifcoverforownplantisrequired") ?? false;
            //    bool hiredPlantCover = cpeEntity.GetAttributeValue<bool?>("lux_pleaseconfirmifcoverforhiredinplantisrequ") ?? false;
            //    bool tempBuildingsCover = cpeEntity.GetAttributeValue<bool?>("lux_pleaseconfirmifcoverfortemporarybuildings") ?? false;
            //    bool employeeToolsCover = cpeEntity.GetAttributeValue<bool?>("lux_pleaseconfirmifcoverforemployeestoolsisre") ?? false;
            //    bool otherItemsCover = cpeEntity.GetAttributeValue<bool?>("lux_pleaseconfirmifcoverforotheritemsisrequir") ?? false;
            //    bool icowCover = cpeEntity.GetAttributeValue<bool?>("lux_pleaseconfirmifcoverforincreasedcostofwor") ?? false;
            //    bool terrorismCover = cpeEntity.GetAttributeValue<bool?>("lux_iscoverrequiredforterrorism") ?? false;

            //    const int SECTION_OWN_PLANT = 972970000;
            //    const int SECTION_HIRED_IN_PLANT = 972970001;
            //    const int SECTION_TEMP_BUILDINGS = 972970002;
            //    const int SECTION_EMP_TOOLS = 972970003;
            //    const int SECTION_OTHER_ITEMS = 972970004;
            //    const int SECTION_ICOW = 972970005;
            //    const int SECTION_TERRORISM = 972970006;

            //    EntityCollection rateItems = service.RetrieveMultiple(new QueryExpression("lux_contractorsplantandequipmentquotepremui")
            //    {
            //        ColumnSet = new ColumnSet("lux_section"),
            //        Criteria = {
            //            Conditions = {
            //                new ConditionExpression("lux_contractorsplantandequipmentquote", ConditionOperator.Equal, cpeQuoteRef.Id)
            //            }
            //        }
            //    });

            //    var ownPlantPremium = rateItems.Entities.FirstOrDefault(x => x.GetAttributeValue<OptionSetValue>("lux_section").Value == SECTION_OWN_PLANT);
            //    var hiredInPlantPremium = rateItems.Entities.FirstOrDefault(x => x.GetAttributeValue<OptionSetValue>("lux_section").Value == SECTION_HIRED_IN_PLANT);
            //    var tempBuildingsPremium = rateItems.Entities.FirstOrDefault(x => x.GetAttributeValue<OptionSetValue>("lux_section").Value == SECTION_TEMP_BUILDINGS);
            //    var empToolsPremium = rateItems.Entities.FirstOrDefault(x => x.GetAttributeValue<OptionSetValue>("lux_section").Value == SECTION_EMP_TOOLS);
            //    var otherItemsPremium = rateItems.Entities.FirstOrDefault(x => x.GetAttributeValue<OptionSetValue>("lux_section").Value == SECTION_OTHER_ITEMS);
            //    var icowPremium = rateItems.Entities.FirstOrDefault(x => x.GetAttributeValue<OptionSetValue>("lux_section").Value == SECTION_ICOW);
            //    var terrorismPremium = rateItems.Entities.FirstOrDefault(x => x.GetAttributeValue<OptionSetValue>("lux_section").Value == SECTION_TERRORISM);

            //    //Decimal totalPremiumBeforeTax = 0;

            //    // Own Plant
            //    if (ownPlantCover)
            //    {
            //        decimal ownPlantValue = cpeEntity.GetAttributeValue<decimal>("lux_totalownplantvalue");
            //        if (ownPlantPremium == null)
            //        {
            //            CreateRecord(service, "Own Plant", SECTION_OWN_PLANT, ownPlantValue, 1, cpeQuoteRef, premiumCurrency);
            //        }
            //        else
            //        {
            //            UpdateRecord(service, ownPlantValue, ownPlantPremium.Id, premiumCurrency);
            //        }
            //    }
            //    else if (ownPlantPremium != null)
            //    {
            //        DeleteRecord(service, ownPlantPremium.Id);
            //    }

            //    // Hired Plant
            //    if (hiredPlantCover)
            //    {
            //        decimal hiredPlantValue = cpeEntity.GetAttributeValue<decimal>("lux_totalhiredplantvalue");
            //        if (hiredInPlantPremium == null)
            //        {
            //            CreateRecord(service, "Hired in plant", SECTION_HIRED_IN_PLANT, hiredPlantValue, 2, cpeQuoteRef, premiumCurrency);
            //        }
            //        else
            //        {
            //            UpdateRecord(service, hiredPlantValue, hiredInPlantPremium.Id, premiumCurrency);
            //        }
            //    }
            //    else if (hiredInPlantPremium != null)
            //    {
            //        DeleteRecord(service, hiredInPlantPremium.Id);
            //    }

            //    // Temporary Buildings
            //    if (tempBuildingsCover)
            //    {
            //        decimal tempBuildingsValue = cpeEntity.GetAttributeValue<decimal>("lux_temporarybuildingssuminsured");
            //        if (tempBuildingsPremium == null)
            //        {
            //            CreateRecord(service, "Temporary Buildings", SECTION_TEMP_BUILDINGS, tempBuildingsValue, 3, cpeQuoteRef, premiumCurrency);
            //        }
            //        else
            //        {
            //            UpdateRecord(service, tempBuildingsValue, tempBuildingsPremium.Id, premiumCurrency);
            //        }
            //    }
            //    else if (tempBuildingsPremium != null)
            //    {
            //        DeleteRecord(service, tempBuildingsPremium.Id);
            //    }

            //    // Employee Tools
            //    if (employeeToolsCover)
            //    {
            //        decimal employeeToolsValue = cpeEntity.GetAttributeValue<decimal>("lux_employeestoolstotalvalue");
            //        if (empToolsPremium == null)
            //        {
            //            CreateRecord(service, "Employees tools", SECTION_EMP_TOOLS, employeeToolsValue, 4, cpeQuoteRef, premiumCurrency);
            //        }
            //        else
            //        {
            //            UpdateRecord(service, employeeToolsValue, empToolsPremium.Id, premiumCurrency);
            //        }
            //    }
            //    else if (empToolsPremium != null)
            //    {
            //        DeleteRecord(service, empToolsPremium.Id);
            //    }

            //    // Other Items
            //    if (otherItemsCover)
            //    {
            //        decimal otherItemsValue = cpeEntity.GetAttributeValue<decimal>("lux_otheritemslimit");
            //        if (otherItemsPremium == null)
            //        {
            //            CreateRecord(service, "Other items", SECTION_OTHER_ITEMS, otherItemsValue, 5, cpeQuoteRef, premiumCurrency);
            //        }
            //        else
            //        {
            //            UpdateRecord(service, otherItemsValue, otherItemsPremium.Id, premiumCurrency);
            //        }
            //    }
            //    else if (otherItemsPremium != null)
            //    {
            //        DeleteRecord(service, otherItemsPremium.Id);
            //    }

            //    // ICOW
            //    if (icowCover)
            //    {
            //        decimal icowLimit = cpeEntity.GetAttributeValue<decimal>("lux_increasedcostofworkinglimitofindemnity");
            //        if (icowPremium == null)
            //        {
            //            CreateRecord(service, "Increased cost of working", SECTION_ICOW, icowLimit, 6, cpeQuoteRef, premiumCurrency);
            //        }
            //        else
            //        {
            //            UpdateRecord(service, icowLimit, icowPremium.Id, premiumCurrency);
            //        }
            //    }
            //    else if (icowPremium != null)
            //    {
            //        DeleteRecord(service, icowPremium.Id);
            //    }

            //    // Terrorism
            //    if (terrorismCover)
            //    {
            //        //decimal cbd = cpeEntity.GetAttributeValue<decimal>("lux_acentralbusinessdistrict");
            //        //decimal metro = cpeEntity.GetAttributeValue<decimal>("lux_bmetropolitan");
            //        //decimal rural = cpeEntity.GetAttributeValue<decimal>("lux_crural");

            //        //decimal totalTerrorismTier = cbd + metro + rural;
            //        if (terrorismPremium == null)
            //        {
            //            CreateRecord(service, "Terrorism", SECTION_TERRORISM, 0, 7, cpeQuoteRef, premiumCurrency);
            //        }
            //        else
            //        {
            //            UpdateRecord(service, 0, terrorismPremium.Id, premiumCurrency);
            //        }
            //    }
            //    else if (terrorismPremium != null)
            //    {
            //        DeleteRecord(service, terrorismPremium.Id);
            //    }

            //    //Broker Changes

            //    //var product = cpeEntity.GetAttributeValue<EntityReference>("lux_product") ?? throw new InvalidPluginExecutionException("Product not found.");
            //    //var broker = cpeEntity.GetAttributeValue<EntityReference>("lux_brokercompanyname") ?? throw new InvalidPluginExecutionException("Broker not found.");

            //    //decimal defaultTotalCommission = 32.5M;
            //    //decimal defaultBrokerCommission = 25M;
            //    //decimal defaultAciesComm = 7.5M;

            //    //EntityCollection brokerCommissions = service.RetrieveMultiple(new QueryExpression("lux_brokercommission")
            //    //{
            //    //    ColumnSet = new ColumnSet("lux_commission", "lux_renewalcommission"),
            //    //    Criteria = {
            //    //        Filters = {
            //    //            new FilterExpression(LogicalOperator.And)
            //    //            {
            //    //                Conditions =
            //    //                {
            //    //                    new ConditionExpression("lux_broker", ConditionOperator.Equal, broker.Id),
            //    //                    new ConditionExpression("lux_product", ConditionOperator.Equal, product.Id),
            //    //                    new ConditionExpression("statecode", ConditionOperator.Equal, 0)
            //    //                },
            //    //                Filters =
            //    //                {
            //    //                    new FilterExpression(LogicalOperator.Or)
            //    //                    {
            //    //                        Conditions =
            //    //                        {
            //    //                            new ConditionExpression("lux_effectivefrom", ConditionOperator.Null),
            //    //                            new ConditionExpression("lux_effectivefrom", ConditionOperator.OnOrBefore, inceptionDate)
            //    //                        }
            //    //                    },
            //    //                    new FilterExpression(LogicalOperator.Or)
            //    //                    {
            //    //                        Conditions =
            //    //                        {
            //    //                            new ConditionExpression("lux_effectiveto", ConditionOperator.Null),
            //    //                            new ConditionExpression("lux_effectiveto", ConditionOperator.OnOrAfter, inceptionDate)
            //    //                        }
            //    //                    }
            //    //                }
            //    //            }
            //    //        }
            //    //    },
            //    //    Orders = { new OrderExpression("createdon", OrderType.Ascending) }
            //    //});

            //    //var appType = cpeEntity.GetAttributeValue<OptionSetValue>("lux_applicationtype")?.Value ?? throw new InvalidPluginExecutionException("Quote application type not found.");

            //    //bool hasTechBrokerComm = cpeEntity.Attributes.Contains("lux_technicalbrokercommissionpercentage");
            //    //bool hasTechAciesComm = cpeEntity.Attributes.Contains("lux_technicalaciesmgucommissionpercentage");

            //    //if (!hasTechBrokerComm && brokerCommissions.Entities.Any())
            //    //{
            //    //    var brokerRecord = brokerCommissions.Entities.First();
            //    //    if (appType == 972970001) // NB
            //    //    {
            //    //        defaultBrokerCommission = brokerRecord.GetAttributeValue<decimal>("lux_commission");
            //    //    }
            //    //    else if (appType == 972970003) // Renewal
            //    //    {
            //    //        defaultBrokerCommission = brokerRecord.GetAttributeValue<decimal>("lux_renewalcommission");
            //    //    }
            //    //}
            //    //else if (hasTechBrokerComm)
            //    //{
            //    //    defaultBrokerCommission = cpeEntity.GetAttributeValue<decimal>("lux_technicalbrokercommissionpercentage");
            //    //}

            //    //if (!hasTechAciesComm && brokerCommissions.Entities.Any())
            //    //{
            //    //    defaultAciesComm = defaultTotalCommission - defaultBrokerCommission;
            //    //}
            //    //else if (hasTechAciesComm)
            //    //{
            //    //    defaultAciesComm = cpeEntity.GetAttributeValue<decimal>("lux_technicalaciesmgucommissionpercentage");
            //    //}

            //    ////Techincal Commission
            //    //cpeEntity["lux_technicalbrokercommissionpercentage"] = defaultBrokerCommission;  //Broker 
            //    //cpeEntity["lux_technicalaciesmgucommissionpercentage"] = defaultAciesComm;    //Acies

            //    ////Policy Commission
            //    //cpeEntity["lux_policybrokercommissionpercentage"] = defaultBrokerCommission; //Broker
            //    //cpeEntity["lux_policyaciesmgucommissionpercentage"] = defaultAciesComm; //Acies


            //    ////Before Tax Premium 
            //    //cpeEntity["lux_technicalpremiumbeforetax"] = totalPremiumBeforeTax;

            //    ////After Tax
            //    //cpeEntity["lux_policypremiumbeforetax"] = totalPremiumBeforeTax;
            //    //service.Update(cpeEntity);

            //}
            //catch (Exception ex)
            //{
            //    tracingService.Trace("Exception: {0}", ex.ToString());
            //    throw new InvalidPluginExecutionException($"An error occurred while calculating premium. {ex.InnerException?.Message ?? ex.Message}");
            //}
        }

        //private void CreateRecord(IOrganizationService service, string name, int section, decimal ratingFigure, int rowOrder, EntityReference cpeQuoteReference, EntityReference premiumCurrency)
        //{
        //    Entity premiumEntity = new Entity("lux_contractorsplantandequipmentquotepremui");
        //    premiumEntity["lux_name"] = name;
        //    premiumEntity["lux_section"] = new OptionSetValue(section);
        //    premiumEntity["lux_ratingfigures"] = new Money(ratingFigure);
        //    premiumEntity["lux_roworder"] = rowOrder;
        //    premiumEntity["lux_contractorsplantandequipmentquote"] = cpeQuoteReference;
        //    premiumEntity["transactioncurrencyid"] = premiumCurrency;
        //    service.Create(premiumEntity);
        //}

        //private void UpdateRecord(IOrganizationService service, decimal ratingFigure, Guid updateRecordId, EntityReference premiumCurrency)
        //{
        //    Entity premiumEntity = new Entity("lux_contractorsplantandequipmentquotepremui", updateRecordId);
        //    premiumEntity["lux_ratingfigures"] = new Money(ratingFigure);
        //    premiumEntity["transactioncurrencyid"] = premiumCurrency;
        //    service.Update(premiumEntity);
        //}

        //private void DeleteRecord(IOrganizationService service, Guid deleteRecordId)
        //{
        //    service.Delete("lux_contractorsplantandequipmentquotepremui", deleteRecordId);
        //}
    }
}
