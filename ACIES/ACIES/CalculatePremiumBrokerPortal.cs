using Microsoft.Crm.Sdk.Messages;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;
using Microsoft.Xrm.Sdk.Metadata;
using Microsoft.Xrm.Sdk.Query;
using Microsoft.Xrm.Sdk.Workflow;
using System;
using System.Activities;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace ACIES
{
    public class CalculatePremiumBrokerPortal : CodeActivity
    {
        [RequiredArgument]
        [Input("Application")]
        [ReferenceTarget("lux_propertyownersapplications")]
        public InArgument<EntityReference> Application { get; set; }

        [RequiredArgument]
        [Input("Product")]
        public InArgument<string> Product { get; set; }

        protected override void Execute(CodeActivityContext executionContext)
        {
            ITracingService tracingService = executionContext.GetExtension<ITracingService>();
            tracingService.Trace("Application Started");

            //Create the context
            IWorkflowContext context = executionContext.GetExtension<IWorkflowContext>();
            IOrganizationServiceFactory serviceFactory = executionContext.GetExtension<IOrganizationServiceFactory>();
            IOrganizationService service = serviceFactory.CreateOrganizationService(context.UserId);

            var request = new RetrieveCurrentOrganizationRequest();
            var organzationResponse = (RetrieveCurrentOrganizationResponse)service.Execute(request);
            var uriString = organzationResponse.Detail.UrlName;

            bool IsLive = true;

            if (uriString.ToLower().Contains("uat"))
            {
                IsLive = false;
            }

            EntityReference applnref = Application.Get<EntityReference>(executionContext);
            Entity appln = new Entity(applnref.LogicalName, applnref.Id);
            appln = service.Retrieve("lux_propertyownersapplications", applnref.Id, new ColumnSet(true));
            CalculatePOPremium(appln, service, IsLive);
        }

        public static string CalculatePOPremium(Entity appln, IOrganizationService service, bool IsLive)
        {
            try
            {
                var productData = service.Retrieve("product", appln.GetAttributeValue<EntityReference>("lux_insuranceproductrequired").Id, new ColumnSet(true));
                var ApplicationType = appln.GetAttributeValue<OptionSetValue>("lux_applicationtype").Value;
                var productName = productData.Attributes["name"].ToString();
                var Broker = service.Retrieve("account", appln.GetAttributeValue<EntityReference>("lux_broker").Id, new ColumnSet(true));
                var IsStudentHolidayLetTenant = false;
                var IsLeisureTrade = false;
                var productId = productData.Id;
                var dateDiffDays = (appln.GetAttributeValue<DateTime>("lux_renewaldate") - appln.GetAttributeValue<DateTime>("lux_inceptiondate")).Days;
                if (dateDiffDays == 363 || dateDiffDays == 364 || dateDiffDays == 365 || dateDiffDays == 366 || dateDiffDays == 367)
                {
                    dateDiffDays = 365;
                }
                var quotationDate = appln.Contains("lux_quotationdate") ? appln.GetAttributeValue<DateTime>("lux_quotationdate") : appln.GetAttributeValue<DateTime>("lux_inceptiondate");
                var inceptionDate = Convert.ToDateTime(appln.FormattedValues["lux_inceptiondate"], System.Globalization.CultureInfo.GetCultureInfo("en-GB").DateTimeFormat);

                var fetch = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                              <entity name='lux_propertyownerspremise'>
                                <attribute name='lux_riskpostcode' />
                                <attribute name='lux_riskaddress' />
                                <attribute name='lux_locationnumber' />
                                <attribute name='lux_tenanttype' />                                
                                <attribute name='lux_isthepremisesahouseofmultipleoccupation' />
                                <attribute name='lux_declaredvalueforrebuildingthisproperty' />
                                <attribute name='lux_buildingconstruction' />
                                <attribute name='lux_totalsuminsuredforthislocation' />
                                <attribute name='lux_basisofcover' />                               
                                <attribute name='lux_indexlinkingdayone' />
                                <attribute name='lux_occupancytype' />
                                <attribute name='lux_lossofannualrentalincome' />
                                <attribute name='lux_suminsuredwithupliftedamount' />
                                <attribute name='lux_totalcontentsofcommunalareas' />
                                <attribute name='lux_landlordscontentsinresidentialareas' />
                                <attribute name='lux_totalnumberofcommercialunitsatthisaddress' />
                                <attribute name='lux_howmanyfloorsareofconcreteconstruction' />
                                <attribute name='lux_howmanyfloorsareofwoodenconstruction' />
                                <attribute name='lux_commercialunit1' />
                                <attribute name='lux_commercialunit2' />
                                <attribute name='lux_commercialunit3' />
                                <attribute name='lux_commercialunit4' />
                                <attribute name='lux_commercialunit5' />
                                <attribute name='lux_commercialunit6' />
                                <attribute name='lux_commercialunit7' />
                                <attribute name='lux_commercialunit8' />
                                <attribute name='lux_commercialunit9' />
                                <attribute name='lux_commercialunit10' />
                                <attribute name='lux_commercialunit11' />
                                <attribute name='lux_commercialunit12' />
                                <attribute name='lux_commercialunit13' />
                                <attribute name='lux_commercialunit14' />
                                <attribute name='lux_commercialunit15' />
                                <attribute name='lux_commercialunit16' />
                                <attribute name='lux_commercialunit17' />
                                <attribute name='lux_commercialunit18' />
                                <attribute name='lux_commercialunit19' />
                                <attribute name='lux_commercialunit20' />
                                <attribute name='lux_lossofannualrentalincome' />
                                <attribute name='lux_indemnityperiodrequired' />
                                <attribute name='lux_propertyownerspremiseid' />
                                <attribute name='lux_materialdamagepremium' />
                                <attribute name='lux_materialdamageperilsrate' />
                                <attribute name='lux_covers' />
                                <attribute name='lux_levelofcover' />      
                                <attribute name='lux_businessinterruptionperilsrate' />
                                <attribute name='lux_businessinterruptionpremium' />
                                <attribute name='lux_lengthofexpectedunoccupancy' />
                                <order attribute='lux_riskpostcode' descending='false' />
                                <filter type='and'>
                                  <condition attribute='statecode' operator='eq' value='0' />
                                  <condition attribute='lux_propertyownersapplication' operator='eq' uiname='' uitype='lux_propertyownersapplications' value='{appln.Id}' />
                                </filter>
                                <link-entity name='lux_propertyownersapplications' from='lux_propertyownersapplicationsid' to='lux_propertyownersapplication' visible='false' link-type='outer' alias='poa'>
                                  <attribute name='lux_levelofcommission' />
                                  <attribute name='lux_whatisyourtrade' />
                                  <attribute name='lux_rpocpoproducttype' />
                                </link-entity>
                              </entity>
                            </fetch>";

                if (service.RetrieveMultiple(new FetchExpression(fetch)).Entities.Count > 0)
                {
                    var premises = service.RetrieveMultiple(new FetchExpression(fetch)).Entities;
                    var NoOfLocations = premises.Count;

                    decimal TotalMDPremium = 0;
                    decimal TotalBIPremium = 0;
                    decimal TotalELPremium = 0;
                    decimal TotalPOLPremium = 0;
                    decimal TotalLENetPremium = 0;
                    decimal TotalLEGrossPremium = 0;
                    decimal MDPerilRate = 0;

                    decimal BrokerComm = 25;
                    decimal aciesComm = 10;
                    decimal LiabilityaciesComm = 5;

                    var rpoProductType = appln.Attributes.Contains("lux_rpocpoproducttype") ? appln.GetAttributeValue<OptionSetValue>("lux_rpocpoproducttype").Value : 0;

                    if (rpoProductType == 972970003 || rpoProductType == 972970004)
                    {
                        productId = new Guid("5a439c84-febd-eb11-bacc-000d3ad6a20a");
                    }

                    var BrokerFetch = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                                          <entity name='lux_brokercommission'>
                                            <attribute name='createdon' />
                                            <attribute name='lux_product' />
                                            <attribute name='lux_commission' />
                                            <attribute name='lux_brokercommissionid' />
                                            <order attribute='createdon' descending='false' />
                                            <filter type='and'>
                                              <condition attribute='statecode' operator='eq' value='0' />
                                              <filter type='or'>
                                                <condition attribute='lux_effectivefrom' operator='on-or-before' value='{String.Format("{0:MM/dd/yyyy}", inceptionDate)}' />
                                                <condition attribute='lux_effectivefrom' operator='null' />
                                              </filter>
                                              <filter type='or'>
                                                <condition attribute='lux_effectiveto' operator='on-or-after' value= '{String.Format("{0:MM/dd/yyyy}", inceptionDate)}' />
                                                <condition attribute='lux_effectiveto' operator='null' />
                                              </filter>
                                              <condition attribute='lux_broker' operator='eq' uiname='' uitype='account' value='{Broker.Id}' />
                                              <condition attribute='lux_product' operator='eq' uiname='' uitype='product' value='{productData.Id}' />
                                            </filter>
                                          </entity>
                                        </fetch>";
                    if (service.RetrieveMultiple(new FetchExpression(BrokerFetch)).Entities.Count > 0)
                    {
                        BrokerComm = service.RetrieveMultiple(new FetchExpression(BrokerFetch)).Entities[0].GetAttributeValue<decimal>("lux_commission");
                        aciesComm = 35 - BrokerComm;
                        LiabilityaciesComm = 30 - BrokerComm;
                    }

                    decimal totaltechnicalcommission = BrokerComm + aciesComm;
                    decimal totalLiabilitytechnicalcommission = BrokerComm + LiabilityaciesComm;

                    decimal PolicyBrokerComm = appln.Contains("lux_policybrokercommission") ? Convert.ToDecimal(appln.Attributes["lux_policybrokercommission"].ToString().Replace("%", "")) : BrokerComm;
                    decimal PolicyaciesComm = appln.Contains("lux_policyaciescommission") ? Convert.ToDecimal(appln.Attributes["lux_policyaciescommission"].ToString().Replace("%", "")) : aciesComm;
                    decimal PolicyLiabilityaciesComm = PolicyBrokerComm + PolicyaciesComm <= 30 ? PolicyaciesComm : 30 - PolicyBrokerComm;

                    decimal totalpolicycommission = PolicyBrokerComm + PolicyaciesComm;
                    decimal totalLiabilitypolicycommission = PolicyBrokerComm + PolicyLiabilityaciesComm;

                    foreach (var item in service.RetrieveMultiple(new FetchExpression(fetch)).Entities)
                    {
                        var premise_data = item;
                        var covers = premise_data.GetAttributeValue<OptionSetValueCollection>("lux_covers");

                        var TenentType = item.Attributes.Contains("lux_tenanttype") ? item.GetAttributeValue<OptionSetValue>("lux_tenanttype").Value : 0;
                        var OccupancyType = item.Attributes.Contains("lux_occupancytype") ? item.GetAttributeValue<OptionSetValue>("lux_occupancytype").Value : 0;

                        if (((OccupancyType == 972970002 || OccupancyType == 972970004)) && (TenentType == 972970009))
                        {
                            IsStudentHolidayLetTenant = true;
                        }

                        var BuildingDeclaredValue = premise_data.Contains("lux_declaredvalueforrebuildingthisproperty") ? premise_data.GetAttributeValue<Money>("lux_declaredvalueforrebuildingthisproperty").Value : 0;
                        var ContentsCommunalValue = premise_data.Contains("lux_totalcontentsofcommunalareas") ? premise_data.GetAttributeValue<Money>("lux_totalcontentsofcommunalareas").Value : 0;
                        var ContentsDeclaredValue = premise_data.Contains("lux_landlordscontentsinresidentialareas") ? premise_data.GetAttributeValue<Money>("lux_landlordscontentsinresidentialareas").Value : 0;
                        var LORSum_insured = premise_data.GetAttributeValue<Money>("lux_lossofannualrentalincome");
                        var LORSum_insuredValue = premise_data.GetAttributeValue<Money>("lux_lossofannualrentalincome") != null ? premise_data.GetAttributeValue<Money>("lux_lossofannualrentalincome").Value : 0;
                        var LOR_indemnity = premise_data.GetAttributeValue<OptionSetValue>("lux_indemnityperiodrequired");

                        var sum_Insured = BuildingDeclaredValue + ContentsCommunalValue + ContentsDeclaredValue + LORSum_insuredValue;

                        decimal MDFireRate = 0;
                        decimal MDTheftRate = 0;
                        decimal SI_rate = 0;
                        decimal TotalRate = 0;
                        decimal GrossRate = 0;
                        var Trade = "";
                        if (appln.GetAttributeValue<OptionSetValue>("lux_rpocpoproducttype").Value == 972970001 || appln.GetAttributeValue<OptionSetValue>("lux_rpocpoproducttype").Value == 972970002)
                        {
                            if (item.GetAttributeValue<OptionSetValue>("lux_occupancytype").Value == 972970002 || item.GetAttributeValue<OptionSetValue>("lux_occupancytype").Value == 972970003) //residential and unoccupied
                            {
                                Trade = "Property Owner - Residential";
                                if (appln.Contains("lux_whatisyourtrade"))
                                {
                                    if (appln.GetAttributeValue<OptionSetValue>("lux_whatisyourtrade").Value == 972970001)
                                    {
                                        Trade = "Housing Association";
                                    }
                                    else if (appln.GetAttributeValue<OptionSetValue>("lux_whatisyourtrade").Value == 972970002)
                                    {
                                        Trade = "Property Developer";
                                    }
                                    else if (appln.GetAttributeValue<OptionSetValue>("lux_whatisyourtrade").Value == 972970003)
                                    {
                                        Trade = "Property Owner - Residential";
                                    }
                                    else if (appln.GetAttributeValue<OptionSetValue>("lux_whatisyourtrade").Value == 972970004)
                                    {
                                        Trade = "Property Owner - Residential";
                                    }
                                    else if (appln.GetAttributeValue<OptionSetValue>("lux_whatisyourtrade").Value == 972970005)
                                    {
                                        Trade = "Property Management";
                                    }
                                }

                                var FireRateFetch = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                                              <entity name='lux_propertyownersrate'>
                                                <attribute name='lux_workaway' />
                                                <attribute name='lux_transitratesendings' />
                                                <attribute name='lux_transitrateownvehicle' />
                                                <attribute name='lux_tradesegment' />
                                                <attribute name='lux_tradesector' />
                                                <attribute name='lux_theftstockrate' />
                                                <attribute name='lux_theftcontentsrate' />
                                                <attribute name='lux_theftbyemployeetradebaserate' />
                                                <attribute name='lux_theft' />
                                                <attribute name='lux_productsrate' />
                                                <attribute name='lux_prods' />
                                                <attribute name='lux_plworkawaywagesrate' />
                                                <attribute name='lux_plpremiserate' />
                                                <attribute name='lux_mdbi' />
                                                <attribute name='lux_mdfirerate' />
                                                <attribute name='lux_fulldescription' />
                                                <attribute name='lux_elrate' />
                                                <attribute name='lux_blfirerate' />
                                                <attribute name='lux_el' />
                                                <attribute name='lux_propertyownersrateid' />
                                                <order attribute='lux_blfirerate' descending='false' />
                                                <filter type='and'>
                                                  <condition attribute='statecode' operator='eq' value='0' />
                                                  <condition attribute='lux_startdate' operator='on-or-before' value='{String.Format("{0:MM/dd/yyyy}", quotationDate)}' />
                                                  <filter type='or'>
                                                    <condition attribute='lux_enddate' operator='on-or-after' value='{String.Format("{0:MM/dd/yyyy}", quotationDate)}' />
                                                    <condition attribute='lux_enddate' operator='null' />
                                                  </filter>
                                                  <condition attribute='lux_name' operator='eq' uiname='' value='{Trade}' />
                                                </filter>
                                              </entity>
                                            </fetch>";

                                if (service.RetrieveMultiple(new FetchExpression(FireRateFetch)).Entities.Count > 0)
                                {
                                    var FireData = service.RetrieveMultiple(new FetchExpression(FireRateFetch)).Entities[0];
                                    MDFireRate = FireData.GetAttributeValue<decimal>("lux_mdfirerate");
                                    MDTheftRate = FireData.GetAttributeValue<decimal>("lux_theftcontentsrate");

                                    if (MDFireRate < 0.10M)
                                    {
                                        MDFireRate = 0.10M;
                                    }

                                    if (appln.GetAttributeValue<OptionSetValue>("lux_rpocpoproducttype").Value == 972970001)
                                    {
                                        MDFireRate = 0.09M;
                                    }

                                    var timberCount = 0;
                                    decimal ConstructionLoad = 0;
                                    decimal TenantLoad = 0;
                                    decimal ConsructionFireRate = MDFireRate;

                                    var timber = premise_data.Contains("lux_howmanyfloorsareofwoodenconstruction") ? premise_data.Attributes["lux_howmanyfloorsareofwoodenconstruction"].ToString() : "";

                                    if (timber != "" && !timber.Contains("0"))
                                    {
                                        timberCount = 1;
                                    }

                                    if (timberCount == 1)
                                    {
                                        var ConstructionFetch = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                                                                  <entity name='lux_floorconstructionloaddiscount'>
                                                                    <attribute name='lux_name' />
                                                                    <attribute name='createdon' />
                                                                    <attribute name='lux_loaddiscount' />
                                                                    <attribute name='lux_floorconstructionloaddiscountid' />
                                                                    <order attribute='lux_name' descending='false' />
                                                                    <filter type='and'>
                                                                      <condition attribute='statecode' operator='eq' value='0' />
                                                                      <condition attribute='lux_startdate' operator='on-or-before' value='{String.Format("{0:MM/dd/yyyy}", quotationDate)}' />
                                                                      <filter type='or'>
                                                                        <condition attribute='lux_enddate' operator='on-or-after' value='{String.Format("{0:MM/dd/yyyy}", quotationDate)}' />
                                                                        <condition attribute='lux_enddate' operator='null' />
                                                                      </filter>
                                                                      <condition attribute='lux_floorconstruction' operator='eq' value='972970001' />
                                                                    </filter>
                                                                  </entity>
                                                                </fetch>";
                                        if (service.RetrieveMultiple(new FetchExpression(ConstructionFetch)).Entities.Count > 0)
                                        {
                                            var LoadData = service.RetrieveMultiple(new FetchExpression(ConstructionFetch)).Entities[0];
                                            ConstructionLoad = LoadData.GetAttributeValue<decimal>("lux_loaddiscount");
                                        }
                                        ConsructionFireRate = MDFireRate + MDFireRate * ConstructionLoad / 100;
                                    }

                                    var tenantType = premise_data.Contains("lux_tenanttype") ? premise_data.GetAttributeValue<OptionSetValue>("lux_tenanttype").Value : 0;
                                    if (tenantType != 0)
                                    {
                                        var TenantFetch = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                                                              <entity name='lux_tenanttypeloaddiscount'>
                                                                <attribute name='lux_name' />
                                                                <attribute name='createdon' />
                                                                <attribute name='lux_loaddiscount' />
                                                                <attribute name='lux_tenanttypeloaddiscountid' />
                                                                <order attribute='lux_name' descending='false' />
                                                                <filter type='and'>
                                                                  <condition attribute='statecode' operator='eq' value='0' />
                                                                  <condition attribute='lux_startdate' operator='on-or-before' value='{String.Format("{0:MM/dd/yyyy}", quotationDate)}' />
                                                                  <filter type='or'>
                                                                    <condition attribute='lux_enddate' operator='on-or-after' value='{String.Format("{0:MM/dd/yyyy}", quotationDate)}' />
                                                                    <condition attribute='lux_enddate' operator='null' />
                                                                  </filter>
                                                                  <condition attribute='lux_tenanttype' operator='eq' value='{tenantType}' />
                                                                </filter>
                                                              </entity>
                                                            </fetch>";
                                        if (service.RetrieveMultiple(new FetchExpression(TenantFetch)).Entities.Count > 0)
                                        {
                                            var LoadData = service.RetrieveMultiple(new FetchExpression(TenantFetch)).Entities[0];
                                            TenantLoad = LoadData.GetAttributeValue<decimal>("lux_loaddiscount");
                                        }
                                    }

                                    var HMO = premise_data.Contains("lux_isthepremisesahouseofmultipleoccupation") ? premise_data.GetAttributeValue<bool>("lux_isthepremisesahouseofmultipleoccupation") : false;
                                    if (HMO == true)
                                    {
                                        var TenantFetch = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                                                              <entity name='lux_tenanttypeloaddiscount'>
                                                                <attribute name='lux_name' />
                                                                <attribute name='createdon' />
                                                                <attribute name='lux_loaddiscount' />
                                                                <attribute name='lux_tenanttypeloaddiscountid' />
                                                                <order attribute='lux_name' descending='false' />
                                                                <filter type='and'>
                                                                  <condition attribute='statecode' operator='eq' value='0' />
                                                                  <condition attribute='lux_startdate' operator='on-or-before' value='{String.Format("{0:MM/dd/yyyy}", quotationDate)}' />
                                                                  <filter type='or'>
                                                                    <condition attribute='lux_enddate' operator='on-or-after' value='{String.Format("{0:MM/dd/yyyy}", quotationDate)}' />
                                                                    <condition attribute='lux_enddate' operator='null' />
                                                                  </filter>
                                                                  <condition attribute='lux_tenanttype' operator='eq' value='{972970008}' />
                                                                </filter>
                                                              </entity>
                                                            </fetch>";
                                        if (service.RetrieveMultiple(new FetchExpression(TenantFetch)).Entities.Count > 0)
                                        {
                                            var LoadData = service.RetrieveMultiple(new FetchExpression(TenantFetch)).Entities[0];
                                            TenantLoad += LoadData.GetAttributeValue<decimal>("lux_loaddiscount");
                                        }
                                    }
                                    GrossRate = ConsructionFireRate + ConsructionFireRate * TenantLoad / 100;
                                }
                            }
                            else if (item.GetAttributeValue<OptionSetValue>("lux_occupancytype").Value == 972970004) //commercial with residential
                            {
                                var numberofTrades = item.Attributes.Contains("lux_totalnumberofcommercialunitsatthisaddress") ? item.GetAttributeValue<int>("lux_totalnumberofcommercialunitsatthisaddress") : 0;
                                decimal FireRate = 0;
                                decimal TheftRate = 0;
                                decimal FireRateBI = 0;
                                if (numberofTrades > 0)
                                {
                                    for (int i = 1; i <= numberofTrades; i++)
                                    {
                                        var fieldName = "lux_commercialunit" + i;
                                        Trade = item.FormattedValues[fieldName].ToString();

                                        var FireRateFetch = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                                              <entity name='lux_propertyownersrate'>
                                                <attribute name='lux_workaway' />
                                                <attribute name='lux_transitratesendings' />
                                                <attribute name='lux_transitrateownvehicle' />
                                                <attribute name='lux_tradesegment' />
                                                <attribute name='lux_tradesector' />
                                                <attribute name='lux_theftstockrate' />
                                                <attribute name='lux_theftcontentsrate' />
                                                <attribute name='lux_theftbyemployeetradebaserate' />
                                                <attribute name='lux_theft' />
                                                <attribute name='lux_productsrate' />
                                                <attribute name='lux_prods' />
                                                <attribute name='lux_plworkawaywagesrate' />
                                                <attribute name='lux_plpremiserate' />
                                                <attribute name='lux_mdbi' />
                                                <attribute name='lux_mdfirerate' />
                                                <attribute name='lux_fulldescription' />
                                                <attribute name='lux_documenttemplate' />
                                                <attribute name='lux_elrate' />
                                                <attribute name='lux_blfirerate' />
                                                <attribute name='lux_el' />
                                                <attribute name='lux_propertyownersrateid' />
                                                <order attribute='lux_blfirerate' descending='false' />
                                                <filter type='and'>
                                                  <condition attribute='statecode' operator='eq' value='0' />
                                                  <condition attribute='lux_startdate' operator='on-or-before' value='{String.Format("{0:MM/dd/yyyy}", quotationDate)}' />
                                                  <filter type='or'>
                                                    <condition attribute='lux_enddate' operator='on-or-after' value='{String.Format("{0:MM/dd/yyyy}", quotationDate)}' />
                                                    <condition attribute='lux_enddate' operator='null' />
                                                  </filter>
                                                  <condition attribute='lux_name' operator='eq' uiname='' value='{Trade}' />
                                                </filter>
                                              </entity>
                                            </fetch>";

                                        if (service.RetrieveMultiple(new FetchExpression(FireRateFetch)).Entities.Count > 0)
                                        {
                                            var FireData = service.RetrieveMultiple(new FetchExpression(FireRateFetch)).Entities[0];
                                            FireRate += FireData.GetAttributeValue<decimal>("lux_mdfirerate") < 0.10M ? 0.10M : FireData.GetAttributeValue<decimal>("lux_mdfirerate");
                                            FireRateBI += FireData.GetAttributeValue<decimal>("lux_blfirerate") < 0.06M ? 0.06M : FireData.GetAttributeValue<decimal>("lux_blfirerate");
                                            TheftRate += FireData.GetAttributeValue<decimal>("lux_theftcontentsrate");

                                            var tradeSector = FireData.FormattedValues["lux_tradesector"].ToString();
                                            var BinderTemplate = FireData.Attributes.Contains("lux_documenttemplate") ? FireData.FormattedValues["lux_documenttemplate"].ToString() : "";
                                            if (tradeSector.Contains("Leisure") || BinderTemplate == "HCC")
                                            {
                                                IsLeisureTrade = true;
                                            }
                                        }
                                    }

                                    MDFireRate = FireRate / numberofTrades;
                                    MDTheftRate = TheftRate / numberofTrades;

                                    if (MDFireRate < 0.10M)
                                    {
                                        MDFireRate = 0.10M;
                                    }

                                    if (appln.GetAttributeValue<OptionSetValue>("lux_rpocpoproducttype").Value == 972970001)
                                    {
                                        MDFireRate = 0.09M;
                                    }

                                    var timberCount = 0;
                                    decimal ConstructionLoad = 0;
                                    decimal TenantLoad = 0;
                                    decimal ConsructionFireRate = MDFireRate;

                                    var timber = premise_data.Contains("lux_howmanyfloorsareofwoodenconstruction") ? premise_data.Attributes["lux_howmanyfloorsareofwoodenconstruction"].ToString() : "";

                                    if (timber != "" && !timber.Contains("0"))
                                    {
                                        timberCount = 1;
                                    }

                                    if (timberCount == 1)
                                    {
                                        var ConstructionFetch = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                                                                  <entity name='lux_floorconstructionloaddiscount'>
                                                                    <attribute name='lux_name' />
                                                                    <attribute name='createdon' />
                                                                    <attribute name='lux_loaddiscount' />
                                                                    <attribute name='lux_floorconstructionloaddiscountid' />
                                                                    <order attribute='lux_name' descending='false' />
                                                                    <filter type='and'>
                                                                      <condition attribute='statecode' operator='eq' value='0' />
                                                                      <condition attribute='lux_startdate' operator='on-or-before' value='{String.Format("{0:MM/dd/yyyy}", quotationDate)}' />
                                                                      <filter type='or'>
                                                                        <condition attribute='lux_enddate' operator='on-or-after' value='{String.Format("{0:MM/dd/yyyy}", quotationDate)}' />
                                                                        <condition attribute='lux_enddate' operator='null' />
                                                                      </filter>
                                                                      <condition attribute='lux_floorconstruction' operator='eq' value='972970001' />
                                                                    </filter>
                                                                  </entity>
                                                                </fetch>";
                                        if (service.RetrieveMultiple(new FetchExpression(ConstructionFetch)).Entities.Count > 0)
                                        {
                                            var LoadData = service.RetrieveMultiple(new FetchExpression(ConstructionFetch)).Entities[0];
                                            ConstructionLoad = LoadData.GetAttributeValue<decimal>("lux_loaddiscount");
                                        }
                                        ConsructionFireRate = MDFireRate + MDFireRate * ConstructionLoad / 100;
                                    }

                                    var tenantType = premise_data.Contains("lux_tenanttype") ? premise_data.GetAttributeValue<OptionSetValue>("lux_tenanttype").Value : 0;
                                    if (tenantType != 0)
                                    {
                                        var TenantFetch = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                                                              <entity name='lux_tenanttypeloaddiscount'>
                                                                <attribute name='lux_name' />
                                                                <attribute name='createdon' />
                                                                <attribute name='lux_loaddiscount' />
                                                                <attribute name='lux_tenanttypeloaddiscountid' />
                                                                <order attribute='lux_name' descending='false' />
                                                                <filter type='and'>
                                                                  <condition attribute='statecode' operator='eq' value='0' />
                                                                  <condition attribute='lux_startdate' operator='on-or-before' value='{String.Format("{0:MM/dd/yyyy}", quotationDate)}' />
                                                                  <filter type='or'>
                                                                    <condition attribute='lux_enddate' operator='on-or-after' value='{String.Format("{0:MM/dd/yyyy}", quotationDate)}' />
                                                                    <condition attribute='lux_enddate' operator='null' />
                                                                  </filter>
                                                                  <condition attribute='lux_tenanttype' operator='eq' value='{tenantType}' />
                                                                </filter>
                                                              </entity>
                                                            </fetch>";
                                        if (service.RetrieveMultiple(new FetchExpression(TenantFetch)).Entities.Count > 0)
                                        {
                                            var LoadData = service.RetrieveMultiple(new FetchExpression(TenantFetch)).Entities[0];
                                            TenantLoad = LoadData.GetAttributeValue<decimal>("lux_loaddiscount");
                                        }
                                    }

                                    var HMO = premise_data.Contains("lux_isthepremisesahouseofmultipleoccupation") ? premise_data.GetAttributeValue<bool>("lux_isthepremisesahouseofmultipleoccupation") : false;
                                    if (HMO == true)
                                    {
                                        var TenantFetch = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                                                              <entity name='lux_tenanttypeloaddiscount'>
                                                                <attribute name='lux_name' />
                                                                <attribute name='createdon' />
                                                                <attribute name='lux_loaddiscount' />
                                                                <attribute name='lux_tenanttypeloaddiscountid' />
                                                                <order attribute='lux_name' descending='false' />
                                                                <filter type='and'>
                                                                  <condition attribute='statecode' operator='eq' value='0' />
                                                                  <condition attribute='lux_startdate' operator='on-or-before' value='{String.Format("{0:MM/dd/yyyy}", quotationDate)}' />
                                                                  <filter type='or'>
                                                                    <condition attribute='lux_enddate' operator='on-or-after' value='{String.Format("{0:MM/dd/yyyy}", quotationDate)}' />
                                                                    <condition attribute='lux_enddate' operator='null' />
                                                                  </filter>
                                                                  <condition attribute='lux_tenanttype' operator='eq' value='{972970008}' />
                                                                </filter>
                                                              </entity>
                                                            </fetch>";
                                        if (service.RetrieveMultiple(new FetchExpression(TenantFetch)).Entities.Count > 0)
                                        {
                                            var LoadData = service.RetrieveMultiple(new FetchExpression(TenantFetch)).Entities[0];
                                            TenantLoad += LoadData.GetAttributeValue<decimal>("lux_loaddiscount");
                                        }
                                    }
                                    GrossRate = ConsructionFireRate + ConsructionFireRate * TenantLoad / 100;
                                }
                            }
                            else if (item.GetAttributeValue<OptionSetValue>("lux_occupancytype").Value == 972970001) //commercial
                            {
                                var numberofTrades = item.Attributes.Contains("lux_totalnumberofcommercialunitsatthisaddress") ? item.GetAttributeValue<int>("lux_totalnumberofcommercialunitsatthisaddress") : 0;
                                decimal FireRate = 0;
                                decimal TheftRate = 0;
                                decimal FireRateBI = 0;
                                if (numberofTrades > 0)
                                {
                                    for (int i = 1; i <= numberofTrades; i++)
                                    {
                                        var fieldName = "lux_commercialunit" + i;
                                        Trade = item.FormattedValues[fieldName].ToString();

                                        var FireRateFetch = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                                              <entity name='lux_propertyownersrate'>
                                                <attribute name='lux_workaway' />
                                                <attribute name='lux_transitratesendings' />
                                                <attribute name='lux_transitrateownvehicle' />
                                                <attribute name='lux_tradesegment' />
                                                <attribute name='lux_tradesector' />
                                                <attribute name='lux_theftstockrate' />
                                                <attribute name='lux_theftcontentsrate' />
                                                <attribute name='lux_theftbyemployeetradebaserate' />
                                                <attribute name='lux_theft' />
                                                <attribute name='lux_productsrate' />
                                                <attribute name='lux_prods' />
                                                <attribute name='lux_plworkawaywagesrate' />
                                                <attribute name='lux_plpremiserate' />
                                                <attribute name='lux_mdbi' />
                                                <attribute name='lux_mdfirerate' />
                                                <attribute name='lux_fulldescription' />
                                                <attribute name='lux_documenttemplate' />
                                                <attribute name='lux_elrate' />
                                                <attribute name='lux_blfirerate' />
                                                <attribute name='lux_el' />
                                                <attribute name='lux_propertyownersrateid' />
                                                <order attribute='lux_blfirerate' descending='false' />
                                                <filter type='and'>
                                                  <condition attribute='statecode' operator='eq' value='0' />
                                                  <condition attribute='lux_startdate' operator='on-or-before' value='{String.Format("{0:MM/dd/yyyy}", quotationDate)}' />
                                                  <filter type='or'>
                                                    <condition attribute='lux_enddate' operator='on-or-after' value='{String.Format("{0:MM/dd/yyyy}", quotationDate)}' />
                                                    <condition attribute='lux_enddate' operator='null' />
                                                  </filter>
                                                  <condition attribute='lux_name' operator='eq' uiname='' value='{Trade}' />
                                                </filter>
                                              </entity>
                                            </fetch>";

                                        if (service.RetrieveMultiple(new FetchExpression(FireRateFetch)).Entities.Count > 0)
                                        {
                                            var FireData = service.RetrieveMultiple(new FetchExpression(FireRateFetch)).Entities[0];

                                            TheftRate += FireData.GetAttributeValue<decimal>("lux_theftcontentsrate");
                                            FireRate += FireData.GetAttributeValue<decimal>("lux_mdfirerate") < 0.10M ? 0.10M : FireData.GetAttributeValue<decimal>("lux_mdfirerate");
                                            FireRateBI += FireData.GetAttributeValue<decimal>("lux_blfirerate") < 0.06M ? 0.06M : FireData.GetAttributeValue<decimal>("lux_blfirerate");

                                            var tradeSector = FireData.FormattedValues["lux_tradesector"].ToString();
                                            var BinderTemplate = FireData.Attributes.Contains("lux_documenttemplate") ? FireData.FormattedValues["lux_documenttemplate"].ToString() : "";
                                            if (tradeSector.Contains("Leisure") || BinderTemplate == "HCC")
                                            {
                                                IsLeisureTrade = true;
                                            }
                                        }
                                    }

                                    MDFireRate = FireRate / numberofTrades;
                                    MDTheftRate = TheftRate / numberofTrades;

                                    if (MDFireRate < 0.10M)
                                    {
                                        MDFireRate = 0.10M;
                                    }

                                    if (appln.GetAttributeValue<OptionSetValue>("lux_rpocpoproducttype").Value == 972970001)
                                    {
                                        MDFireRate = 0.09M;
                                    }

                                    var timberCount = 0;
                                    decimal ConstructionLoad = 0;
                                    decimal ConsructionFireRate = MDFireRate;

                                    var timber = premise_data.Contains("lux_howmanyfloorsareofwoodenconstruction") ? premise_data.Attributes["lux_howmanyfloorsareofwoodenconstruction"].ToString() : "";

                                    if (timber != "" && !timber.Contains("0"))
                                    {
                                        timberCount = 1;
                                    }

                                    if (timberCount == 1)
                                    {
                                        var ConstructionFetch = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                                                                  <entity name='lux_floorconstructionloaddiscount'>
                                                                    <attribute name='lux_name' />
                                                                    <attribute name='createdon' />
                                                                    <attribute name='lux_loaddiscount' />
                                                                    <attribute name='lux_floorconstructionloaddiscountid' />
                                                                    <order attribute='lux_name' descending='false' />
                                                                    <filter type='and'>
                                                                      <condition attribute='statecode' operator='eq' value='0' />
                                                                      <condition attribute='lux_startdate' operator='on-or-before' value='{String.Format("{0:MM/dd/yyyy}", quotationDate)}' />
                                                                      <filter type='or'>
                                                                        <condition attribute='lux_enddate' operator='on-or-after' value='{String.Format("{0:MM/dd/yyyy}", quotationDate)}' />
                                                                        <condition attribute='lux_enddate' operator='null' />
                                                                      </filter>
                                                                      <condition attribute='lux_floorconstruction' operator='eq' value='972970001' />
                                                                    </filter>
                                                                  </entity>
                                                                </fetch>";
                                        if (service.RetrieveMultiple(new FetchExpression(ConstructionFetch)).Entities.Count > 0)
                                        {
                                            var LoadData = service.RetrieveMultiple(new FetchExpression(ConstructionFetch)).Entities[0];
                                            ConstructionLoad = LoadData.GetAttributeValue<decimal>("lux_loaddiscount");
                                        }
                                        ConsructionFireRate = MDFireRate + MDFireRate * ConstructionLoad / 100;
                                    }
                                    GrossRate = ConsructionFireRate;
                                }
                            }
                        }
                        else
                        {
                            var FireRateFetch = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                                              <entity name='lux_propertyownersrate'>
                                                <attribute name='lux_workaway' />
                                                <attribute name='lux_transitratesendings' />
                                                <attribute name='lux_transitrateownvehicle' />
                                                <attribute name='lux_tradesegment' />
                                                <attribute name='lux_tradesector' />
                                                <attribute name='lux_theftstockrate' />
                                                <attribute name='lux_theftcontentsrate' />
                                                <attribute name='lux_theftbyemployeetradebaserate' />
                                                <attribute name='lux_theft' />
                                                <attribute name='lux_productsrate' />
                                                <attribute name='lux_prods' />
                                                <attribute name='lux_plworkawaywagesrate' />
                                                <attribute name='lux_plpremiserate' />
                                                <attribute name='lux_mdbi' />
                                                <attribute name='lux_mdfirerate' />
                                                <attribute name='lux_fulldescription' />
                                                <attribute name='lux_elrate' />
                                                <attribute name='lux_blfirerate' />
                                                <attribute name='lux_el' />
                                                <attribute name='lux_propertyownersrateid' />
                                                <order attribute='lux_blfirerate' descending='false' />
                                                <filter type='and'>
                                                  <condition attribute='statecode' operator='eq' value='0' />
                                                  <condition attribute='lux_startdate' operator='on-or-before' value='{String.Format("{0:MM/dd/yyyy}", quotationDate)}' />
                                                  <filter type='or'>
                                                    <condition attribute='lux_enddate' operator='on-or-after' value='{String.Format("{0:MM/dd/yyyy}", quotationDate)}' />
                                                    <condition attribute='lux_enddate' operator='null' />
                                                  </filter>
                                                  <condition attribute='lux_name' operator='eq' uiname='' value='{Trade}' />
                                                </filter>
                                              </entity>
                                            </fetch>";

                            if (service.RetrieveMultiple(new FetchExpression(FireRateFetch)).Entities.Count > 0)
                            {
                                var FireData = service.RetrieveMultiple(new FetchExpression(FireRateFetch)).Entities[0];
                                MDTheftRate = FireData.GetAttributeValue<decimal>("lux_theftcontentsrate");
                            }

                            if (item.GetAttributeValue<OptionSetValue>("lux_occupancytype").Value == 972970002 || item.GetAttributeValue<OptionSetValue>("lux_occupancytype").Value == 972970003) //residential
                            {
                                MDFireRate = 0.20M;

                                var timberCount = 0;
                                decimal ConstructionLoad = 0;
                                decimal TenantLoad = 0;
                                decimal ConsructionFireRate = MDFireRate;

                                var timber = premise_data.Contains("lux_howmanyfloorsareofwoodenconstruction") ? premise_data.Attributes["lux_howmanyfloorsareofwoodenconstruction"].ToString() : "";

                                if (timber != "" && !timber.Contains("0"))
                                {
                                    timberCount = 1;
                                }

                                if (timberCount == 1)
                                {
                                    var ConstructionFetch = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                                                                  <entity name='lux_floorconstructionloaddiscount'>
                                                                    <attribute name='lux_name' />
                                                                    <attribute name='createdon' />
                                                                    <attribute name='lux_loaddiscount' />
                                                                    <attribute name='lux_floorconstructionloaddiscountid' />
                                                                    <order attribute='lux_name' descending='false' />
                                                                    <filter type='and'>
                                                                      <condition attribute='statecode' operator='eq' value='0' />
                                                                      <condition attribute='lux_startdate' operator='on-or-before' value='{String.Format("{0:MM/dd/yyyy}", quotationDate)}' />
                                                                      <filter type='or'>
                                                                        <condition attribute='lux_enddate' operator='on-or-after' value='{String.Format("{0:MM/dd/yyyy}", quotationDate)}' />
                                                                        <condition attribute='lux_enddate' operator='null' />
                                                                      </filter>
                                                                      <condition attribute='lux_floorconstruction' operator='eq' value='972970001' />
                                                                    </filter>
                                                                  </entity>
                                                                </fetch>";
                                    if (service.RetrieveMultiple(new FetchExpression(ConstructionFetch)).Entities.Count > 0)
                                    {
                                        var LoadData = service.RetrieveMultiple(new FetchExpression(ConstructionFetch)).Entities[0];
                                        ConstructionLoad = LoadData.GetAttributeValue<decimal>("lux_loaddiscount");
                                    }
                                    ConsructionFireRate = MDFireRate + MDFireRate * ConstructionLoad / 100;
                                }

                                var coverType = premise_data.Contains("lux_levelofcover") ? premise_data.GetAttributeValue<OptionSetValue>("lux_levelofcover").Value : 0;

                                if (coverType == 972970001)
                                {
                                    TenantLoad = 0;
                                }
                                else if (coverType == 972970002)
                                {
                                    TenantLoad = 25;
                                }
                                else if (coverType == 972970003)
                                {
                                    TenantLoad = 35;
                                }

                                var unoccupancyLength = premise_data.Contains("lux_lengthofexpectedunoccupancy") ? premise_data.GetAttributeValue<OptionSetValue>("lux_lengthofexpectedunoccupancy").Value : 0;

                                if (unoccupancyLength == 972970002 || unoccupancyLength == 972970003)
                                {
                                    TenantLoad += 10;
                                }
                                else if (unoccupancyLength == 972970004 || unoccupancyLength == 972970005 || unoccupancyLength == 972970006)
                                {
                                    TenantLoad += 20;
                                }

                                GrossRate = ConsructionFireRate + ConsructionFireRate * TenantLoad / 100;
                            }
                            else if (item.GetAttributeValue<OptionSetValue>("lux_occupancytype").Value == 972970001 || item.GetAttributeValue<OptionSetValue>("lux_occupancytype").Value == 972970004) //commercial
                            {
                                MDFireRate = 0.25M;

                                var timberCount = 0;
                                decimal ConstructionLoad = 0;
                                decimal TenantLoad = 0;
                                decimal ConsructionFireRate = MDFireRate;

                                var timber = premise_data.Contains("lux_howmanyfloorsareofwoodenconstruction") ? premise_data.Attributes["lux_howmanyfloorsareofwoodenconstruction"].ToString() : "";

                                if (timber != "" && !timber.Contains("0"))
                                {
                                    timberCount = 1;
                                }

                                if (timberCount == 1)
                                {
                                    var ConstructionFetch = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                                                                  <entity name='lux_floorconstructionloaddiscount'>
                                                                    <attribute name='lux_name' />
                                                                    <attribute name='createdon' />
                                                                    <attribute name='lux_loaddiscount' />
                                                                    <attribute name='lux_floorconstructionloaddiscountid' />
                                                                    <order attribute='lux_name' descending='false' />
                                                                    <filter type='and'>
                                                                      <condition attribute='statecode' operator='eq' value='0' />
                                                                      <condition attribute='lux_startdate' operator='on-or-before' value='{String.Format("{0:MM/dd/yyyy}", quotationDate)}' />
                                                                      <filter type='or'>
                                                                        <condition attribute='lux_enddate' operator='on-or-after' value='{String.Format("{0:MM/dd/yyyy}", quotationDate)}' />
                                                                        <condition attribute='lux_enddate' operator='null' />
                                                                      </filter>
                                                                      <condition attribute='lux_floorconstruction' operator='eq' value='972970001' />
                                                                    </filter>
                                                                  </entity>
                                                                </fetch>";
                                    if (service.RetrieveMultiple(new FetchExpression(ConstructionFetch)).Entities.Count > 0)
                                    {
                                        var LoadData = service.RetrieveMultiple(new FetchExpression(ConstructionFetch)).Entities[0];
                                        ConstructionLoad = LoadData.GetAttributeValue<decimal>("lux_loaddiscount");
                                    }
                                    ConsructionFireRate = MDFireRate + MDFireRate * ConstructionLoad / 100;
                                }

                                var coverType = premise_data.Contains("lux_levelofcover") ? premise_data.GetAttributeValue<OptionSetValue>("lux_levelofcover").Value : 0;

                                if (coverType == 972970001)
                                {
                                    TenantLoad = 0;
                                }
                                else if (coverType == 972970002)
                                {
                                    TenantLoad = 25;
                                }
                                else if (coverType == 972970003)
                                {
                                    TenantLoad = 35;
                                }

                                var unoccupancyLength = premise_data.Contains("lux_lengthofexpectedunoccupancy") ? premise_data.GetAttributeValue<OptionSetValue>("lux_lengthofexpectedunoccupancy").Value : 0;

                                if (unoccupancyLength == 972970002 || unoccupancyLength == 972970003)
                                {
                                    TenantLoad += 10;
                                }
                                else if (unoccupancyLength == 972970004 || unoccupancyLength == 972970005 || unoccupancyLength == 972970006)
                                {
                                    TenantLoad += 20;
                                }
                                GrossRate = ConsructionFireRate + ConsructionFireRate * TenantLoad / 100;
                            }
                        }

                        var item1 = service.Retrieve("lux_propertyownerspremise", item.Id, new ColumnSet(true));

                        if (item.GetAttributeValue<OptionSetValue>("lux_basisofcover").Value == 972970001)
                        {
                            var indexingValue = item.Attributes.Contains("lux_indexlinkingdayone") ? item.FormattedValues["lux_indexlinkingdayone"].ToString() : "";
                            if (indexingValue != "")
                            {
                                var indexed = Convert.ToDecimal(indexingValue.Replace("%", ""));
                                var Amount = BuildingDeclaredValue + ContentsDeclaredValue + ContentsCommunalValue;
                                var upliftedAmount = Amount + Amount * indexed / 100 + LORSum_insuredValue;
                                item1["lux_declaredvaluewithupliftedamount"] = new Money(BuildingDeclaredValue + BuildingDeclaredValue * indexed / 100);
                                item1["lux_suminsuredwithupliftedamount"] = new Money(upliftedAmount);
                            }
                        }
                        else
                        {
                            item1["lux_declaredvaluewithupliftedamount"] = new Money(0);
                            item1["lux_suminsuredwithupliftedamount"] = new Money(BuildingDeclaredValue + ContentsCommunalValue + ContentsDeclaredValue + LORSum_insuredValue);
                        }

                        item1["lux_declaredvalueforrebuildingthisproperty"] = new Money(BuildingDeclaredValue);
                        item1["lux_landlordscontentsinresidentialareas"] = new Money(ContentsDeclaredValue);
                        item1["lux_totalcontentsofcommunalareas"] = new Money(ContentsCommunalValue);

                        //MD Premium
                        if (covers != null && sum_Insured > 0)
                        {
                            var TotalSumInsuredFetch = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                                                  <entity name='lux_totalsuminsuredrate'>
                                                    <attribute name='lux_5m25m' />
                                                    <attribute name='lux_50m100m' />
                                                    <attribute name='lux_25m50m' />
                                                    <attribute name='lux_100m200m' />
                                                    <attribute name='lux_05m' />
                                                    <attribute name='lux_totalsuminsuredrateid' />
                                                    <attribute name='lux_peril' />
                                                    <order attribute='lux_05m' descending='false' />
                                                    <filter type='and'>
                                                      <condition attribute='statecode' operator='eq' value='0' />
                                                      <condition attribute='lux_startdate' operator='on-or-before' value='{String.Format("{0:MM/dd/yyyy}", quotationDate)}' />
                                                      <filter type='or'>
                                                        <condition attribute='lux_enddate' operator='on-or-after' value='{String.Format("{0:MM/dd/yyyy}", quotationDate)}' />
                                                        <condition attribute='lux_enddate' operator='null' />
                                                      </filter>";
                            if (covers != null)
                            {
                                TotalSumInsuredFetch += $@"<condition attribute='lux_peril' operator='contain-values'>";
                                foreach (var cover in covers)
                                {
                                    TotalSumInsuredFetch += $@"<value>" + cover.Value + "</value>";
                                }
                                TotalSumInsuredFetch += $@"</condition>";
                            }
                            TotalSumInsuredFetch += $@"</filter>
                                                  </entity>
                                                </fetch>";

                            if (service.RetrieveMultiple(new FetchExpression(TotalSumInsuredFetch)).Entities.Count > 0)
                            {
                                var SI_data = service.RetrieveMultiple(new FetchExpression(TotalSumInsuredFetch)).Entities;
                                var SI_field = "";
                                if (sum_Insured < 5000000)
                                {
                                    SI_field = "lux_05m";
                                }
                                else if (sum_Insured >= 5000000 && sum_Insured < 25000000)
                                {
                                    SI_field = "lux_5m25m";
                                }
                                else if (sum_Insured >= 25000000 && sum_Insured < 50000000)
                                {
                                    SI_field = "lux_25m50m";
                                }
                                else if (sum_Insured >= 50000000 && sum_Insured < 100000000)
                                {
                                    SI_field = "lux_50m100m";
                                }
                                else if (sum_Insured >= 100000000 && sum_Insured < 200000000)
                                {
                                    SI_field = "lux_100m200m";
                                }
                                SI_rate = SI_data.Sum(x => x.GetAttributeValue<decimal>(SI_field)) * 100;
                            }

                            if (IsLive == false)
                            {
                                if (ApplicationType == 972970001)
                                {
                                    if (SI_rate < 0.049M)
                                    {
                                        SI_rate = 0.049M;
                                    }
                                }
                                else
                                {
                                    if (appln.Attributes.Contains("lux_policy"))
                                    {
                                        var Policy = service.Retrieve("lux_policy", appln.GetAttributeValue<EntityReference>("lux_policy").Id, new ColumnSet("lux_policytype"));
                                        if (Policy.GetAttributeValue<OptionSetValue>("lux_policytype").Value == 972970001)
                                        {
                                            if (SI_rate < 0.049M)
                                            {
                                                SI_rate = 0.049M;
                                            }
                                        }
                                    }
                                }
                            }
                            else
                            {
                                if (SI_rate < 0.049M)
                                {
                                    SI_rate = 0.049M;
                                }
                            }

                            TotalRate = GrossRate + SI_rate;

                            var ContentsPremium = 0M;
                            var ContentsRate = 0.15M;

                            ContentsDeclaredValue = ContentsDeclaredValue + ContentsCommunalValue;

                            if (ContentsDeclaredValue != 0 && ApplicationType == 972970001)
                            {
                                if (ContentsDeclaredValue != 0)
                                {
                                    ContentsPremium = ContentsDeclaredValue * ContentsRate / 100;
                                    decimal TheftContentPremium = 0;

                                    if (ContentsDeclaredValue <= 10000)
                                    {
                                        TheftContentPremium = ContentsDeclaredValue * MDTheftRate / 100;
                                    }
                                    else
                                    {
                                        if (ContentsDeclaredValue <= 30000)
                                        {
                                            TheftContentPremium = 10000 * MDTheftRate / 100;
                                            var remainingContent = ContentsDeclaredValue - 10000;
                                            TheftContentPremium += remainingContent * (MDTheftRate * 75 / 100) / 100;
                                        }
                                        else if (ContentsDeclaredValue <= 50000)
                                        {
                                            TheftContentPremium = 10000 * MDTheftRate / 100;
                                            TheftContentPremium += 20000 * (MDTheftRate * 75 / 100) / 100;

                                            var remainingContent1 = ContentsDeclaredValue - 30000;
                                            TheftContentPremium += remainingContent1 * (MDTheftRate * 50 / 100) / 100;
                                        }
                                        else if (ContentsDeclaredValue > 50000)
                                        {
                                            TheftContentPremium = 10000 * MDTheftRate / 100;
                                            TheftContentPremium += 20000 * (MDTheftRate * 75 / 100) / 100;
                                            TheftContentPremium += 20000 * (MDTheftRate * 50 / 100) / 100;

                                            var remainingContent1 = ContentsDeclaredValue - 50000;
                                            TheftContentPremium += remainingContent1 * (MDTheftRate * 25 / 100) / 100;
                                        }
                                    }
                                    ContentsPremium = ContentsPremium + TheftContentPremium;
                                }
                            }
                            else if (ContentsDeclaredValue != 0 && ApplicationType == 972970003)
                            {
                                if (appln.GetAttributeValue<OptionSetValue>("lux_rpocpoproducttype").Value == 972970002)
                                {
                                    if (ContentsDeclaredValue <= 20000)
                                    {
                                        ContentsPremium = ContentsDeclaredValue * ContentsRate / 100;
                                    }
                                    else
                                    {
                                        if (ContentsDeclaredValue <= 60000)
                                        {
                                            ContentsPremium = 20000 * ContentsRate / 100;
                                            var remainingContent = ContentsDeclaredValue - 20000;
                                            ContentsPremium += remainingContent * (ContentsRate * 75 / 100) / 100;
                                        }
                                        else if (ContentsDeclaredValue <= 100000)
                                        {
                                            ContentsPremium = 20000 * ContentsRate / 100;
                                            ContentsPremium += 40000 * (ContentsRate * 75 / 100) / 100;

                                            var remainingContent1 = ContentsDeclaredValue - 60000;
                                            ContentsPremium += remainingContent1 * (ContentsRate * 50 / 100) / 100;
                                        }
                                        else if (ContentsDeclaredValue > 100000)
                                        {
                                            ContentsPremium = 20000 * ContentsRate / 100;
                                            ContentsPremium += 40000 * (ContentsRate * 75 / 100) / 100;
                                            ContentsPremium += 40000 * (ContentsRate * 50 / 100) / 100;

                                            var remainingContent1 = ContentsDeclaredValue - 100000;
                                            ContentsPremium += remainingContent1 * (ContentsRate * 25 / 100) / 100;
                                        }
                                    }
                                }
                                else
                                {
                                    if (ContentsDeclaredValue <= 10000)
                                    {
                                        ContentsPremium = ContentsDeclaredValue * ContentsRate / 100;
                                    }
                                    else
                                    {
                                        if (ContentsDeclaredValue <= 30000)
                                        {
                                            ContentsPremium = 10000 * ContentsRate / 100;
                                            var remainingContent = ContentsDeclaredValue - 10000;
                                            ContentsPremium += remainingContent * (ContentsRate * 75 / 100) / 100;
                                        }
                                        else if (ContentsDeclaredValue <= 50000)
                                        {
                                            ContentsPremium = 10000 * ContentsRate / 100;
                                            ContentsPremium += 20000 * (ContentsRate * 75 / 100) / 100;

                                            var remainingContent1 = ContentsDeclaredValue - 30000;
                                            ContentsPremium += remainingContent1 * (ContentsRate * 50 / 100) / 100;
                                        }
                                        else if (ContentsDeclaredValue > 50000)
                                        {
                                            ContentsPremium = 10000 * ContentsRate / 100;
                                            ContentsPremium += 20000 * (ContentsRate * 75 / 100) / 100;
                                            ContentsPremium += 20000 * (ContentsRate * 50 / 100) / 100;

                                            var remainingContent1 = ContentsDeclaredValue - 50000;
                                            ContentsPremium += remainingContent1 * (ContentsRate * 25 / 100) / 100;
                                        }
                                    }
                                }
                            }
                            else
                            {
                                if (ContentsDeclaredValue != 0 && appln.Attributes.Contains("lux_policy"))
                                {
                                    var Policy = service.Retrieve("lux_policy", appln.GetAttributeValue<EntityReference>("lux_policy").Id, new ColumnSet("lux_policytype"));
                                    if (Policy.GetAttributeValue<OptionSetValue>("lux_policytype").Value == 972970001)
                                    {
                                        if (quotationDate < new DateTime(2023, 02, 15))
                                        {
                                            if (appln.GetAttributeValue<OptionSetValue>("lux_rpocpoproducttype").Value == 972970002)
                                            {
                                                if (ContentsDeclaredValue <= 20000)
                                                {
                                                    ContentsPremium = ContentsDeclaredValue * ContentsRate / 100;
                                                }
                                                else
                                                {
                                                    if (ContentsDeclaredValue <= 60000)
                                                    {
                                                        ContentsPremium = 20000 * ContentsRate / 100;
                                                        var remainingContent = ContentsDeclaredValue - 20000;
                                                        ContentsPremium += remainingContent * (ContentsRate * 75 / 100) / 100;
                                                    }
                                                    else if (ContentsDeclaredValue <= 100000)
                                                    {
                                                        ContentsPremium = 20000 * ContentsRate / 100;
                                                        ContentsPremium += 40000 * (ContentsRate * 75 / 100) / 100;

                                                        var remainingContent1 = ContentsDeclaredValue - 60000;
                                                        ContentsPremium += remainingContent1 * (ContentsRate * 50 / 100) / 100;
                                                    }
                                                    else if (ContentsDeclaredValue > 100000)
                                                    {
                                                        ContentsPremium = 20000 * ContentsRate / 100;
                                                        ContentsPremium += 40000 * (ContentsRate * 75 / 100) / 100;
                                                        ContentsPremium += 40000 * (ContentsRate * 50 / 100) / 100;

                                                        var remainingContent1 = ContentsDeclaredValue - 100000;
                                                        ContentsPremium += remainingContent1 * (ContentsRate * 25 / 100) / 100;
                                                    }
                                                }
                                            }
                                            else
                                            {
                                                if (ContentsDeclaredValue <= 10000)
                                                {
                                                    ContentsPremium = ContentsDeclaredValue * ContentsRate / 100;
                                                }
                                                else
                                                {
                                                    if (ContentsDeclaredValue <= 30000)
                                                    {
                                                        ContentsPremium = 10000 * ContentsRate / 100;
                                                        var remainingContent = ContentsDeclaredValue - 10000;
                                                        ContentsPremium += remainingContent * (ContentsRate * 75 / 100) / 100;
                                                    }
                                                    else if (ContentsDeclaredValue <= 50000)
                                                    {
                                                        ContentsPremium = 10000 * ContentsRate / 100;
                                                        ContentsPremium += 20000 * (ContentsRate * 75 / 100) / 100;

                                                        var remainingContent1 = ContentsDeclaredValue - 30000;
                                                        ContentsPremium += remainingContent1 * (ContentsRate * 50 / 100) / 100;
                                                    }
                                                    else if (ContentsDeclaredValue > 50000)
                                                    {
                                                        ContentsPremium = 10000 * ContentsRate / 100;
                                                        ContentsPremium += 20000 * (ContentsRate * 75 / 100) / 100;
                                                        ContentsPremium += 20000 * (ContentsRate * 50 / 100) / 100;

                                                        var remainingContent1 = ContentsDeclaredValue - 50000;
                                                        ContentsPremium += remainingContent1 * (ContentsRate * 25 / 100) / 100;
                                                    }
                                                }
                                            }
                                        }
                                        else
                                        {
                                            ContentsPremium = ContentsDeclaredValue * ContentsRate / 100;
                                            decimal TheftContentPremium = 0;

                                            if (MDTheftRate != 0)
                                            {
                                                if (ContentsDeclaredValue <= 10000)
                                                {
                                                    TheftContentPremium = ContentsDeclaredValue * MDTheftRate / 100;
                                                }
                                                else
                                                {
                                                    if (ContentsDeclaredValue <= 30000)
                                                    {
                                                        TheftContentPremium = 10000 * MDTheftRate / 100;
                                                        var remainingContent = ContentsDeclaredValue - 10000;
                                                        TheftContentPremium += remainingContent * (MDTheftRate * 75 / 100) / 100;
                                                    }
                                                    else if (ContentsDeclaredValue <= 50000)
                                                    {
                                                        TheftContentPremium = 10000 * MDTheftRate / 100;
                                                        TheftContentPremium += 20000 * (MDTheftRate * 75 / 100) / 100;

                                                        var remainingContent1 = ContentsDeclaredValue - 30000;
                                                        TheftContentPremium += remainingContent1 * (MDTheftRate * 50 / 100) / 100;
                                                    }
                                                    else if (ContentsDeclaredValue > 50000)
                                                    {
                                                        TheftContentPremium = 10000 * MDTheftRate / 100;
                                                        TheftContentPremium += 20000 * (MDTheftRate * 75 / 100) / 100;
                                                        TheftContentPremium += 20000 * (MDTheftRate * 50 / 100) / 100;

                                                        var remainingContent1 = ContentsDeclaredValue - 50000;
                                                        TheftContentPremium += remainingContent1 * (MDTheftRate * 25 / 100) / 100;
                                                    }
                                                }
                                            }
                                            ContentsPremium = ContentsPremium + TheftContentPremium;
                                        }
                                    }
                                    else
                                    {
                                        if (appln.GetAttributeValue<OptionSetValue>("lux_rpocpoproducttype").Value == 972970002)
                                        {
                                            if (ContentsDeclaredValue <= 20000)
                                            {
                                                ContentsPremium = ContentsDeclaredValue * ContentsRate / 100;
                                            }
                                            else
                                            {
                                                if (ContentsDeclaredValue <= 60000)
                                                {
                                                    ContentsPremium = 20000 * ContentsRate / 100;
                                                    var remainingContent = ContentsDeclaredValue - 20000;
                                                    ContentsPremium += remainingContent * (ContentsRate * 75 / 100) / 100;
                                                }
                                                else if (ContentsDeclaredValue <= 100000)
                                                {
                                                    ContentsPremium = 20000 * ContentsRate / 100;
                                                    ContentsPremium += 40000 * (ContentsRate * 75 / 100) / 100;

                                                    var remainingContent1 = ContentsDeclaredValue - 60000;
                                                    ContentsPremium += remainingContent1 * (ContentsRate * 50 / 100) / 100;
                                                }
                                                else if (ContentsDeclaredValue > 100000)
                                                {
                                                    ContentsPremium = 20000 * ContentsRate / 100;
                                                    ContentsPremium += 40000 * (ContentsRate * 75 / 100) / 100;
                                                    ContentsPremium += 40000 * (ContentsRate * 50 / 100) / 100;

                                                    var remainingContent1 = ContentsDeclaredValue - 100000;
                                                    ContentsPremium += remainingContent1 * (ContentsRate * 25 / 100) / 100;
                                                }
                                            }
                                        }
                                        else
                                        {
                                            if (ContentsDeclaredValue <= 10000)
                                            {
                                                ContentsPremium = ContentsDeclaredValue * ContentsRate / 100;
                                            }
                                            else
                                            {
                                                if (ContentsDeclaredValue <= 30000)
                                                {
                                                    ContentsPremium = 10000 * ContentsRate / 100;
                                                    var remainingContent = ContentsDeclaredValue - 10000;
                                                    ContentsPremium += remainingContent * (ContentsRate * 75 / 100) / 100;
                                                }
                                                else if (ContentsDeclaredValue <= 50000)
                                                {
                                                    ContentsPremium = 10000 * ContentsRate / 100;
                                                    ContentsPremium += 20000 * (ContentsRate * 75 / 100) / 100;

                                                    var remainingContent1 = ContentsDeclaredValue - 30000;
                                                    ContentsPremium += remainingContent1 * (ContentsRate * 50 / 100) / 100;
                                                }
                                                else if (ContentsDeclaredValue > 50000)
                                                {
                                                    ContentsPremium = 10000 * ContentsRate / 100;
                                                    ContentsPremium += 20000 * (ContentsRate * 75 / 100) / 100;
                                                    ContentsPremium += 20000 * (ContentsRate * 50 / 100) / 100;

                                                    var remainingContent1 = ContentsDeclaredValue - 50000;
                                                    ContentsPremium += remainingContent1 * (ContentsRate * 25 / 100) / 100;
                                                }
                                            }
                                        }
                                    }
                                }
                            }

                            var MD_Total_Premium = (BuildingDeclaredValue * TotalRate / 100 + ContentsPremium) * dateDiffDays / 365;

                            if (MD_Total_Premium < Convert.ToDecimal(37.5))
                            {
                                MD_Total_Premium = Convert.ToDecimal(37.5);
                                item1["lux_materialdamagepremium"] = new Money(Convert.ToDecimal(37.5));
                            }
                            else
                            {
                                item1["lux_materialdamagepremium"] = new Money(MD_Total_Premium);
                            }
                            MDPerilRate += SI_rate;
                            item1["lux_materialdamageperilsrate"] = Convert.ToDecimal(SI_rate);
                            item1["lux_materialdamagefirerate"] = Convert.ToDecimal(MDFireRate);

                            item1["lux_buildingpremium"] = new Money((BuildingDeclaredValue * TotalRate / 100) * dateDiffDays / 365);
                            item1["lux_contentspremium"] = new Money(ContentsPremium * dateDiffDays / 365);
                            //item1["lux_contentsrate"] = Convert.ToDecimal(0.125); Old Rates
                            item1["lux_contentsrate"] = Convert.ToDecimal(0.15);

                            item1["lux_materialdamageloadedfirerate"] = Convert.ToDecimal(GrossRate);
                            service.Update(item1);

                            TotalMDPremium += MD_Total_Premium;
                        }
                        else
                        {
                            TotalMDPremium += Convert.ToDecimal(37.5);
                            item1["lux_materialdamagepremium"] = new Money(Convert.ToDecimal(0));
                            MDPerilRate += 0;
                            item1["lux_materialdamageperilsrate"] = Convert.ToDecimal(0);
                            item1["lux_materialdamagefirerate"] = Convert.ToDecimal(0);
                            item1["lux_materialdamageloadedfirerate"] = Convert.ToDecimal(0);
                        }

                        //BI Premium
                        decimal PerilsRate = 0;
                        decimal BIFireRate = 0;
                        decimal TotalBIRate = 0;

                        if (item.GetAttributeValue<OptionSetValue>("lux_occupancytype").Value == 972970002 || item.GetAttributeValue<OptionSetValue>("lux_occupancytype").Value == 972970003) //residential and unoccupied
                        {
                            Trade = "Property Owner - Residential";
                            if (appln.Contains("lux_whatisyourtrade"))
                            {
                                if (appln.GetAttributeValue<OptionSetValue>("lux_whatisyourtrade").Value == 972970001)
                                {
                                    Trade = "Housing Association";
                                }
                                else if (appln.GetAttributeValue<OptionSetValue>("lux_whatisyourtrade").Value == 972970002)
                                {
                                    Trade = "Property Developer";
                                }
                                else if (appln.GetAttributeValue<OptionSetValue>("lux_whatisyourtrade").Value == 972970003)
                                {
                                    Trade = "Property Owner - Residential";
                                }
                                else if (appln.GetAttributeValue<OptionSetValue>("lux_whatisyourtrade").Value == 972970004)
                                {
                                    Trade = "Property Owner - Residential";
                                }
                                else if (appln.GetAttributeValue<OptionSetValue>("lux_whatisyourtrade").Value == 972970005)
                                {
                                    Trade = "Property Management";
                                }
                            }

                            var FireRateFetch = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                                              <entity name='lux_propertyownersrate'>
                                                <attribute name='lux_workaway' />
                                                <attribute name='lux_transitratesendings' />
                                                <attribute name='lux_transitrateownvehicle' />
                                                <attribute name='lux_tradesegment' />
                                                <attribute name='lux_tradesector' />
                                                <attribute name='lux_theftstockrate' />
                                                <attribute name='lux_theftcontentsrate' />
                                                <attribute name='lux_theftbyemployeetradebaserate' />
                                                <attribute name='lux_theft' />
                                                <attribute name='lux_productsrate' />
                                                <attribute name='lux_prods' />
                                                <attribute name='lux_plworkawaywagesrate' />
                                                <attribute name='lux_plpremiserate' />
                                                <attribute name='lux_mdbi' />
                                                <attribute name='lux_mdfirerate' />
                                                <attribute name='lux_fulldescription' />
                                                <attribute name='lux_elrate' />
                                                <attribute name='lux_blfirerate' />
                                                <attribute name='lux_el' />
                                                <attribute name='lux_propertyownersrateid' />
                                                <order attribute='lux_blfirerate' descending='false' />
                                                <filter type='and'>
                                                  <condition attribute='statecode' operator='eq' value='0' />
                                                  <condition attribute='lux_startdate' operator='on-or-before' value='{String.Format("{0:MM/dd/yyyy}", quotationDate)}' />
                                                  <filter type='or'>
                                                    <condition attribute='lux_enddate' operator='on-or-after' value='{String.Format("{0:MM/dd/yyyy}", quotationDate)}' />
                                                    <condition attribute='lux_enddate' operator='null' />
                                                  </filter>
                                                  <condition attribute='lux_name' operator='eq' uiname='' value='{Trade}' />
                                                </filter>
                                              </entity>
                                            </fetch>";

                            if (service.RetrieveMultiple(new FetchExpression(FireRateFetch)).Entities.Count > 0)
                            {
                                var FireData = service.RetrieveMultiple(new FetchExpression(FireRateFetch)).Entities[0];
                                BIFireRate = FireData.GetAttributeValue<decimal>("lux_blfirerate");
                                if (BIFireRate < 0.06M)
                                {
                                    BIFireRate = 0.06M;
                                }
                            }
                        }
                        else if (item.GetAttributeValue<OptionSetValue>("lux_occupancytype").Value == 972970004) //commercial with residential
                        {
                            var numberofTrades = item.Attributes.Contains("lux_totalnumberofcommercialunitsatthisaddress") ? item.GetAttributeValue<int>("lux_totalnumberofcommercialunitsatthisaddress") : 0;
                            decimal FireRate = 0;
                            if (numberofTrades > 0)
                            {
                                for (int i = 1; i <= numberofTrades; i++)
                                {
                                    var fieldName = "lux_commercialunit" + i;
                                    Trade = item.FormattedValues[fieldName].ToString();

                                    var FireRateFetch = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                                              <entity name='lux_propertyownersrate'>
                                                <attribute name='lux_workaway' />
                                                <attribute name='lux_transitratesendings' />
                                                <attribute name='lux_transitrateownvehicle' />
                                                <attribute name='lux_tradesegment' />
                                                <attribute name='lux_tradesector' />
                                                <attribute name='lux_theftstockrate' />
                                                <attribute name='lux_theftcontentsrate' />
                                                <attribute name='lux_theftbyemployeetradebaserate' />
                                                <attribute name='lux_theft' />
                                                <attribute name='lux_productsrate' />
                                                <attribute name='lux_prods' />
                                                <attribute name='lux_plworkawaywagesrate' />
                                                <attribute name='lux_plpremiserate' />
                                                <attribute name='lux_mdbi' />
                                                <attribute name='lux_mdfirerate' />
                                                <attribute name='lux_fulldescription' />
                                                <attribute name='lux_elrate' />
                                                <attribute name='lux_blfirerate' />
                                                <attribute name='lux_el' />
                                                <attribute name='lux_propertyownersrateid' />
                                                <order attribute='lux_blfirerate' descending='false' />
                                                <filter type='and'>
                                                  <condition attribute='statecode' operator='eq' value='0' />
                                                  <condition attribute='lux_startdate' operator='on-or-before' value='{String.Format("{0:MM/dd/yyyy}", quotationDate)}' />
                                                  <filter type='or'>
                                                    <condition attribute='lux_enddate' operator='on-or-after' value='{String.Format("{0:MM/dd/yyyy}", quotationDate)}' />
                                                    <condition attribute='lux_enddate' operator='null' />
                                                  </filter>
                                                  <condition attribute='lux_name' operator='eq' uiname='' value='{Trade}' />
                                                </filter>
                                              </entity>
                                            </fetch>";

                                    if (service.RetrieveMultiple(new FetchExpression(FireRateFetch)).Entities.Count > 0)
                                    {
                                        var FireData = service.RetrieveMultiple(new FetchExpression(FireRateFetch)).Entities[0];
                                        FireRate += FireData.GetAttributeValue<decimal>("lux_blfirerate") < 0.06M ? 0.06M : FireData.GetAttributeValue<decimal>("lux_blfirerate");
                                    }
                                }
                                BIFireRate = FireRate / numberofTrades;
                                if (BIFireRate < 0.06M)
                                {
                                    BIFireRate = 0.06M;
                                }
                            }
                        }
                        else if (item.GetAttributeValue<OptionSetValue>("lux_occupancytype").Value == 972970001) //commercial
                        {
                            var numberofTrades = item.Attributes.Contains("lux_totalnumberofcommercialunitsatthisaddress") ? item.GetAttributeValue<int>("lux_totalnumberofcommercialunitsatthisaddress") : 0;
                            decimal FireRate = 0;
                            if (numberofTrades > 0)
                            {
                                for (int i = 1; i <= numberofTrades; i++)
                                {
                                    var fieldName = "lux_commercialunit" + i;
                                    Trade = item.FormattedValues[fieldName].ToString();

                                    var FireRateFetch = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                                              <entity name='lux_propertyownersrate'>
                                                <attribute name='lux_workaway' />
                                                <attribute name='lux_transitratesendings' />
                                                <attribute name='lux_transitrateownvehicle' />
                                                <attribute name='lux_tradesegment' />
                                                <attribute name='lux_tradesector' />
                                                <attribute name='lux_theftstockrate' />
                                                <attribute name='lux_theftcontentsrate' />
                                                <attribute name='lux_theftbyemployeetradebaserate' />
                                                <attribute name='lux_theft' />
                                                <attribute name='lux_productsrate' />
                                                <attribute name='lux_prods' />
                                                <attribute name='lux_plworkawaywagesrate' />
                                                <attribute name='lux_plpremiserate' />
                                                <attribute name='lux_mdbi' />
                                                <attribute name='lux_mdfirerate' />
                                                <attribute name='lux_fulldescription' />
                                                <attribute name='lux_elrate' />
                                                <attribute name='lux_blfirerate' />
                                                <attribute name='lux_el' />
                                                <attribute name='lux_propertyownersrateid' />
                                                <order attribute='lux_blfirerate' descending='false' />
                                                <filter type='and'>
                                                  <condition attribute='statecode' operator='eq' value='0' />
                                                  <condition attribute='lux_startdate' operator='on-or-before' value='{String.Format("{0:MM/dd/yyyy}", quotationDate)}' />
                                                  <filter type='or'>
                                                    <condition attribute='lux_enddate' operator='on-or-after' value='{String.Format("{0:MM/dd/yyyy}", quotationDate)}' />
                                                    <condition attribute='lux_enddate' operator='null' />
                                                  </filter>
                                                  <condition attribute='lux_name' operator='eq' uiname='' value='{Trade}' />
                                                </filter>
                                              </entity>
                                            </fetch>";

                                    if (service.RetrieveMultiple(new FetchExpression(FireRateFetch)).Entities.Count > 0)
                                    {
                                        var FireData = service.RetrieveMultiple(new FetchExpression(FireRateFetch)).Entities[0];
                                        FireRate += FireData.GetAttributeValue<decimal>("lux_blfirerate") < 0.06M ? 0.06M : FireData.GetAttributeValue<decimal>("lux_blfirerate");
                                    }
                                }
                                BIFireRate = FireRate / numberofTrades;
                                if (BIFireRate < 0.06M)
                                {
                                    BIFireRate = 0.06M;
                                }
                            }
                        }

                        if (LORSum_insured != null && LOR_indemnity != null && LORSum_insured.Value != 0)
                        {
                            var IndemnityPeriod = "";
                            if (LOR_indemnity.Value == 972970001)
                            {
                                IndemnityPeriod = "12";
                            }
                            else if (LOR_indemnity.Value == 972970002)
                            {
                                IndemnityPeriod = "18";
                            }
                            else if (LOR_indemnity.Value == 972970003)
                            {
                                IndemnityPeriod = "24";
                            }
                            else if (LOR_indemnity.Value == 972970004)
                            {
                                IndemnityPeriod = "36";
                            }

                            var TotalLossOfRentFetch = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                                                          <entity name='lux_lossofrentrate'>
                                                            <attribute name='lux_name' />
                                                            <attribute name='lux_rating4' />
                                                            <attribute name='lux_rating3' />
                                                            <attribute name='lux_rating2' />
                                                            <attribute name='lux_rating1' />
                                                            <attribute name='lux_lossofrentrateid' />
                                                            <order attribute='lux_name' descending='false' />
                                                            <filter type='and'>
                                                              <condition attribute='statecode' operator='eq' value='0' />
                                                              <condition attribute='lux_name' operator='eq' value='{IndemnityPeriod}' />
                                                            </filter>
                                                          </entity>
                                                        </fetch>";
                            if (service.RetrieveMultiple(new FetchExpression(TotalLossOfRentFetch)).Entities.Count > 0)
                            {
                                var rating = "";
                                var LOR_Rate = service.RetrieveMultiple(new FetchExpression(TotalLossOfRentFetch)).Entities[0];
                                if (NoOfLocations > 0 && NoOfLocations <= 5)
                                {
                                    rating = "lux_rating1";
                                }
                                else if (NoOfLocations >= 6 && NoOfLocations <= 20)
                                {
                                    rating = "lux_rating2";
                                }
                                else if (NoOfLocations >= 21 && NoOfLocations <= 50)
                                {
                                    rating = "lux_rating3";
                                }
                                else if (NoOfLocations >= 51)
                                {
                                    rating = "lux_rating4";
                                }
                                var LORRate = LOR_Rate.GetAttributeValue<decimal>(rating);
                                //PerilsRate = Convert.ToDecimal(0.5) * AvgPerils * LORRate; Old Rates
                                PerilsRate = 0.03M;
                            }

                            if (item.GetAttributeValue<OptionSetValue>("lux_occupancytype").Value == 972970002) //residential
                            {
                                TotalBIRate = BIFireRate + PerilsRate;
                            }
                            else
                            {
                                TotalBIRate = BIFireRate + PerilsRate;
                            }
                            var LORPremium = (LORSum_insured.Value * TotalBIRate / 100) * dateDiffDays / 365;

                            if (LORPremium < Convert.ToDecimal(12.5) && appln.GetAttributeValue<OptionSetValue>("lux_applicationtype").Value != 972970001)
                            {
                                if (appln.GetAttributeValue<OptionSetValue>("lux_applicationtype").Value == 972970003)
                                {
                                    LORPremium = Convert.ToDecimal(12.5);
                                    item1["lux_businessinterruptionpremium"] = new Money(Convert.ToDecimal(12.5));
                                }
                                else
                                {
                                    if (appln.Attributes.Contains("lux_policy"))
                                    {
                                        var Policy = service.Retrieve("lux_policy", appln.GetAttributeValue<EntityReference>("lux_policy").Id, new ColumnSet("lux_policytype"));
                                        if (Policy.GetAttributeValue<OptionSetValue>("lux_policytype").Value == 972970002)
                                        {
                                            LORPremium = Convert.ToDecimal(12.5);
                                            item1["lux_businessinterruptionpremium"] = new Money(Convert.ToDecimal(12.5));
                                        }
                                    }
                                }
                            }
                            else
                            {
                                item1["lux_businessinterruptionpremium"] = new Money(LORPremium);
                            }
                            item1["lux_businessinterruptionperilsrate"] = PerilsRate;
                            item1["lux_businessinterruptionfirerate"] = BIFireRate;
                            item1["lux_businessinterruptionloadedfirerate"] = Convert.ToDecimal(BIFireRate);
                            service.Update(item1);

                            TotalBIPremium += Convert.ToDecimal(LORPremium);
                        }
                        else
                        {
                            if (inceptionDate < new DateTime(2022, 04, 05) || quotationDate < new DateTime(2022, 04, 05))
                            {
                                TotalBIPremium += Convert.ToDecimal(12.5);
                                item1["lux_businessinterruptionpremium"] = new Money(Convert.ToDecimal(12.5));
                            }
                            else
                            {
                                TotalBIPremium += Convert.ToDecimal(0);
                                item1["lux_businessinterruptionpremium"] = new Money(Convert.ToDecimal(0));
                            }

                            item1["lux_businessinterruptionperilsrate"] = Convert.ToDecimal(0);
                            item1["lux_businessinterruptionfirerate"] = Convert.ToDecimal(0);
                            item1["lux_businessinterruptionloadedfirerate"] = Convert.ToDecimal(0);
                            service.Update(item1);
                        }
                        service.Update(item1);
                    }

                    if (TotalMDPremium < 75)
                    {
                        TotalMDPremium = 75;
                    }

                    if (inceptionDate < new DateTime(2022, 04, 05) || quotationDate < new DateTime(2022, 04, 05))
                    {
                        if (TotalBIPremium < 25)
                        {
                            TotalBIPremium = 25;
                        }
                    }
                    else
                    {
                        if (TotalBIPremium > 0 && TotalBIPremium < 25 && appln.GetAttributeValue<OptionSetValue>("lux_applicationtype").Value != 972970001)
                        {
                            if (appln.GetAttributeValue<OptionSetValue>("lux_applicationtype").Value == 972970003)
                            {
                                TotalBIPremium = 25;
                            }
                            else
                            {
                                if (appln.Attributes.Contains("lux_policy"))
                                {
                                    var Policy = service.Retrieve("lux_policy", appln.GetAttributeValue<EntityReference>("lux_policy").Id, new ColumnSet("lux_policytype"));
                                    if (Policy.GetAttributeValue<OptionSetValue>("lux_policytype").Value == 972970002)
                                    {
                                        TotalBIPremium = 25;
                                    }
                                }
                            }
                        }
                    }

                    //Terrorism Premium
                    decimal TerrorismTotal = 0;
                    if (appln.Attributes.Contains("lux_isterrorismcoverrequired") && appln.GetAttributeValue<bool>("lux_isterrorismcoverrequired") == true)
                    {
                        foreach (var item in service.RetrieveMultiple(new FetchExpression(fetch)).Entities)
                        {
                            var premise_data = item;
                            var postcode = premise_data.Contains("lux_riskpostcode") ? premise_data.Attributes["lux_riskpostcode"] : "";
                            var post2digits = postcode.ToString().Substring(0, 2);
                            var post3digits = postcode.ToString().Substring(0, 3);
                            var post4digits = postcode.ToString().Substring(0, 4);
                            var zone = 972970003;
                            if (postcode.ToString() != "")
                            {
                                var TerrorismFetch = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                                                          <entity name='lux_terrorismratingzone'>
                                                            <attribute name='lux_name' />
                                                            <attribute name='lux_locationzone' />
                                                            <attribute name='lux_terrorismratingzoneid' />
                                                            <order attribute='lux_locationzone' descending='false' />
                                                            <order attribute='lux_name' descending='false' />
                                                            <filter type='and'>
                                                              <condition attribute='statecode' operator='eq' value='0' />
                                                              <condition attribute='lux_name' operator='eq' value='{post4digits}' />
                                                            </filter>
                                                          </entity>
                                                        </fetch>";
                                if (service.RetrieveMultiple(new FetchExpression(TerrorismFetch)).Entities.Count > 0)
                                {
                                    zone = service.RetrieveMultiple(new FetchExpression(TerrorismFetch)).Entities[0].GetAttributeValue<OptionSetValue>("lux_locationzone").Value;
                                }
                                else
                                {
                                    var TerrorismFetch1 = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                                                          <entity name='lux_terrorismratingzone'>
                                                            <attribute name='lux_name' />
                                                            <attribute name='lux_locationzone' />
                                                            <attribute name='lux_terrorismratingzoneid' />
                                                            <order attribute='lux_locationzone' descending='false' />
                                                            <order attribute='lux_name' descending='false' />
                                                            <filter type='and'>
                                                              <condition attribute='statecode' operator='eq' value='0' />
                                                              <condition attribute='lux_name' operator='eq' value='{post3digits}' />
                                                            </filter>
                                                          </entity>
                                                        </fetch>";
                                    if (service.RetrieveMultiple(new FetchExpression(TerrorismFetch1)).Entities.Count > 0)
                                    {
                                        zone = service.RetrieveMultiple(new FetchExpression(TerrorismFetch1)).Entities[0].GetAttributeValue<OptionSetValue>("lux_locationzone").Value;
                                    }
                                    else
                                    {
                                        var TerrorismFetch2 = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                                                          <entity name='lux_terrorismratingzone'>
                                                            <attribute name='lux_name' />
                                                            <attribute name='lux_locationzone' />
                                                            <attribute name='lux_terrorismratingzoneid' />
                                                            <order attribute='lux_locationzone' descending='false' />
                                                            <order attribute='lux_name' descending='false' />
                                                            <filter type='and'>
                                                              <condition attribute='statecode' operator='eq' value='0' />
                                                              <condition attribute='lux_name' operator='eq' value='{post2digits}' />
                                                            </filter>
                                                          </entity>
                                                        </fetch>";
                                        if (service.RetrieveMultiple(new FetchExpression(TerrorismFetch2)).Entities.Count > 0)
                                        {
                                            zone = service.RetrieveMultiple(new FetchExpression(TerrorismFetch2)).Entities[0].GetAttributeValue<OptionSetValue>("lux_locationzone").Value;
                                        }
                                    }
                                }
                            }

                            var sum_Insured = premise_data.Contains("lux_totalsuminsuredforthislocation") ? premise_data.GetAttributeValue<Money>("lux_totalsuminsuredforthislocation").Value : 0;
                            var BISum_insured = premise_data.GetAttributeValue<Money>("lux_lossofannualrentalincome").Value;
                            var MDSum_insured = sum_Insured - BISum_insured;

                            decimal TerrorismPremium = 0;
                            decimal TerrorismMDPremium = 0;
                            decimal TerrorismBIPremium = 0;
                            decimal MDSI_rate = 0;
                            decimal BISI_rate = 0;

                            if (MDSum_insured > 0)
                            {
                                var MDRatesFetch = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                                                  <entity name='lux_terrorismrate'>
                                                    <attribute name='lux_ratebeforeanydiscount' />
                                                    <attribute name='lux_locationzone' />
                                                    <attribute name='lux_ratetype' />
                                                    <attribute name='lux_terrorismrateid' />
                                                    <order attribute='lux_ratetype' descending='false' />
                                                    <order attribute='lux_locationzone' descending='false' />
                                                    <filter type='and'>
                                                      <condition attribute='statecode' operator='eq' value='0' />
                                                      <condition attribute='lux_locationzone' operator='eq' value='{zone}' />
                                                      <condition attribute='lux_ratetype' operator='eq' value='972970002' />
                                                    </filter>
                                                  </entity>
                                                </fetch>";

                                if (service.RetrieveMultiple(new FetchExpression(MDRatesFetch)).Entities.Count > 0)
                                {
                                    var SI_data = service.RetrieveMultiple(new FetchExpression(MDRatesFetch)).Entities[0];
                                    if (SI_data.Contains("lux_ratebeforeanydiscount"))
                                    {
                                        MDSI_rate = SI_data.GetAttributeValue<decimal>("lux_ratebeforeanydiscount");
                                    }
                                    TerrorismMDPremium = MDSum_insured * MDSI_rate / 100;
                                }
                            }
                            if (BISum_insured > 0)
                            {
                                var BIRatesFetch = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                                                  <entity name='lux_terrorismrate'>
                                                    <attribute name='lux_ratebeforeanydiscount' />
                                                    <attribute name='lux_locationzone' />
                                                    <attribute name='lux_ratetype' />
                                                    <attribute name='lux_terrorismrateid' />
                                                    <order attribute='lux_ratetype' descending='false' />
                                                    <order attribute='lux_locationzone' descending='false' />
                                                    <filter type='and'>
                                                      <condition attribute='statecode' operator='eq' value='0' />
                                                      <condition attribute='lux_locationzone' operator='eq' value='{zone}' />
                                                      <condition attribute='lux_ratetype' operator='eq' value='972970001' />
                                                    </filter>
                                                  </entity>
                                                </fetch>";

                                if (service.RetrieveMultiple(new FetchExpression(BIRatesFetch)).Entities.Count > 0)
                                {
                                    var SI_data = service.RetrieveMultiple(new FetchExpression(BIRatesFetch)).Entities[0];
                                    if (SI_data.Contains("lux_ratebeforeanydiscount"))
                                    {
                                        BISI_rate = SI_data.GetAttributeValue<decimal>("lux_ratebeforeanydiscount");
                                    }
                                    TerrorismBIPremium = BISum_insured * BISI_rate / 100;
                                }
                            }
                            TerrorismPremium = TerrorismMDPremium + TerrorismBIPremium;

                            var item1 = service.Retrieve("lux_propertyownerspremise", item.Id, new ColumnSet(true));
                            item1["lux_terrorismbipremium"] = new Money(TerrorismBIPremium);
                            item1["lux_terrorismmdpremium"] = new Money(TerrorismMDPremium);
                            item1["lux_terrorismbirate"] = BISI_rate;
                            item1["lux_terrorismmdrate"] = MDSI_rate;
                            item1["lux_terrorismpremium"] = new Money(TerrorismPremium);
                            item1["lux_terrorismzone"] = new OptionSetValue(zone);
                            service.Update(item1);

                            TerrorismTotal += TerrorismPremium;
                        }

                        if (appln.GetAttributeValue<OptionSetValue>("lux_rpocpoproducttype").Value == 972970001 || appln.GetAttributeValue<OptionSetValue>("lux_rpocpoproducttype").Value == 972970003)
                        {
                            if (TerrorismTotal < 35)
                            {
                                TerrorismTotal = 35;
                            }
                        }
                        else if (appln.GetAttributeValue<OptionSetValue>("lux_rpocpoproducttype").Value == 972970002 || appln.GetAttributeValue<OptionSetValue>("lux_rpocpoproducttype").Value == 972970004)
                        {
                            if (TerrorismTotal < 50)
                            {
                                TerrorismTotal = 50;
                            }
                        }

                        if (inceptionDate >= new DateTime(2025, 05, 01))
                        {
                            appln["lux_terrorismbrokercommission"] = "22.5%";
                            appln["lux_terrorismpolicybrokercommission"] = "22.5%";
                            appln["lux_terrorismaciescommission"] = "15.0%";
                            appln["lux_terrorismpolicyaciescommission"] = "15.0%";
                            appln["lux_terrorismtotalcommission"] = "37.5%";
                            appln["lux_terrorismpolicytotalcommission"] = "37.5%";

                            appln["lux_terrorismpremium"] = new Money(TerrorismTotal);
                            appln["lux_terrorismnetpremium"] = new Money(TerrorismTotal - TerrorismTotal * Convert.ToDecimal(37.5) / 100);

                            appln["lux_terrorismbrokercommissionamount"] = new Money(TerrorismTotal * 22.5M / 100);
                            appln["lux_terrorismaciescommissionamout"] = new Money(TerrorismTotal * 15M / 100);

                            appln["lux_terrorismquotedpremium"] = new Money(TerrorismTotal);
                            appln["lux_terrorismpolicynetpremiumexcludingipt"] = new Money(TerrorismTotal - TerrorismTotal * Convert.ToDecimal(37.5) / 100);

                            appln["lux_terrorismquotedpremiumbrokercommissionamo"] = new Money(TerrorismTotal * 22.5M / 100);
                            appln["lux_terrorismquotedpremiumaciescommissionamou"] = new Money(TerrorismTotal * 15M / 100);

                            if (appln.GetAttributeValue<OptionSetValue>("lux_rpocpoproducttype").Value == 972970001)
                            {
                                if (ApplicationType == 972970001)
                                {
                                    appln["lux_terrorismsectiondiscount"] = -30M;
                                    TerrorismTotal = TerrorismTotal - TerrorismTotal * 30 / 100;
                                }
                                if (TerrorismTotal < 35)
                                {
                                    TerrorismTotal = 35;
                                }

                                appln["lux_terrorismquotedpremium"] = new Money(TerrorismTotal);
                                appln["lux_terrorismpolicynetpremiumexcludingipt"] = new Money(TerrorismTotal - TerrorismTotal * Convert.ToDecimal(37.5) / 100);

                                appln["lux_terrorismquotedpremiumbrokercommissionamo"] = new Money(TerrorismTotal * 22.5M / 100);
                                appln["lux_terrorismquotedpremiumaciescommissionamou"] = new Money(TerrorismTotal * 15M / 100);
                            }
                        }
                        else
                        {
                            appln["lux_terrorismbrokercommission"] = "20%";
                            appln["lux_terrorismpolicybrokercommission"] = "20%";
                            appln["lux_terrorismaciescommission"] = "12.5%";
                            appln["lux_terrorismpolicyaciescommission"] = "12.5%";
                            appln["lux_terrorismtotalcommission"] = "32.5%";
                            appln["lux_terrorismpolicytotalcommission"] = "32.5%";

                            appln["lux_terrorismpremium"] = new Money(TerrorismTotal);
                            appln["lux_terrorismnetpremium"] = new Money(TerrorismTotal - TerrorismTotal * Convert.ToDecimal(32.5) / 100);

                            appln["lux_terrorismbrokercommissionamount"] = new Money(TerrorismTotal * 20 / 100);
                            appln["lux_terrorismaciescommissionamout"] = new Money(TerrorismTotal * 12.5M / 100);

                            appln["lux_terrorismquotedpremium"] = new Money(TerrorismTotal);
                            appln["lux_terrorismpolicynetpremiumexcludingipt"] = new Money(TerrorismTotal - TerrorismTotal * Convert.ToDecimal(32.5) / 100);

                            appln["lux_terrorismquotedpremiumbrokercommissionamo"] = new Money(TerrorismTotal * 20 / 100);
                            appln["lux_terrorismquotedpremiumaciescommissionamou"] = new Money(TerrorismTotal * 12.5M / 100);

                            if (appln.GetAttributeValue<OptionSetValue>("lux_rpocpoproducttype").Value == 972970001)
                            {
                                if (ApplicationType == 972970001)
                                {
                                    appln["lux_terrorismsectiondiscount"] = -30M;
                                    TerrorismTotal = TerrorismTotal - TerrorismTotal * 30 / 100;
                                }

                                if (TerrorismTotal < 35)
                                {
                                    TerrorismTotal = 35;
                                }

                                appln["lux_terrorismquotedpremium"] = new Money(TerrorismTotal);
                                appln["lux_terrorismpolicynetpremiumexcludingipt"] = new Money(TerrorismTotal - TerrorismTotal * Convert.ToDecimal(32.5) / 100);

                                appln["lux_terrorismquotedpremiumbrokercommissionamo"] = new Money(TerrorismTotal * 20 / 100);
                                appln["lux_terrorismquotedpremiumaciescommissionamou"] = new Money(TerrorismTotal * 12.5M / 100);
                            }
                        }

                    }

                    //EL Premium
                    if (appln.GetAttributeValue<bool>("lux_iselcoverrequired") == true)
                    {
                        var clerical = appln.Contains("lux_clericalcommercialandmanagerialwageroll") ? appln.GetAttributeValue<Money>("lux_clericalcommercialandmanagerialwageroll").Value : 0;
                        var caretakers = appln.Contains("lux_caretakerscleanersporterswageroll") ? appln.GetAttributeValue<Money>("lux_caretakerscleanersporterswageroll").Value : 0;
                        var alterations = appln.Contains("lux_alterationmaintenancerepairwageroll") ? appln.GetAttributeValue<Money>("lux_alterationmaintenancerepairwageroll").Value : 0;

                        //var clericalGrossRate = Convert.ToDecimal(0.3); Old Rate
                        var clericalGrossRate = Convert.ToDecimal(0.1);
                        var caretakersGrossRate = Convert.ToDecimal(0.5);
                        var alterationsGrossRate = Convert.ToDecimal(1);

                        var clericalPremium = clerical * clericalGrossRate / 100;
                        var caretakersPremium = caretakers * caretakersGrossRate / 100;
                        var alterationsPremium = alterations * alterationsGrossRate / 100;

                        var totalPremium = (clericalPremium + caretakersPremium + alterationsPremium) * dateDiffDays / 365;

                        if (inceptionDate >= new DateTime(2023, 11, 01))
                        {
                            totalPremium = 1.1M * totalPremium;
                        }

                        if (quotationDate >= new DateTime(2025, 01, 01))
                        {
                            if (ApplicationType == 972970001)
                            {
                                totalPremium = 1.06M * totalPremium;
                            }
                            else if (ApplicationType == 972970002 || ApplicationType == 972970004)
                            {
                                if (appln.Attributes.Contains("lux_policy"))
                                {
                                    var Policy = service.Retrieve("lux_policy", appln.GetAttributeValue<EntityReference>("lux_policy").Id, new ColumnSet("lux_policytype"));
                                    if (Policy.GetAttributeValue<OptionSetValue>("lux_policytype").Value == 972970001)
                                    {
                                        totalPremium = 1.06M * totalPremium;
                                    }
                                }
                            }
                        }

                        //if (totalPremium < 25 && appln.GetAttributeValue<OptionSetValue>("lux_applicationtype").Value != 972970001)
                        //{
                        //    totalPremium = 25;
                        //}

                        if (totalPremium < 25)
                        {
                            totalPremium = 25;
                        }

                        TotalELPremium = totalPremium;
                        appln["lux_employersliabilitypremium"] = new Money(totalPremium);
                    }
                    else
                    {
                        TotalELPremium = 0;
                        appln["lux_employersliabilitypremium"] = new Money(0);
                    }


                    //POL Premium
                    CalculateRollupFieldRequest calculateRollup = new CalculateRollupFieldRequest();
                    calculateRollup.FieldName = "lux_totalsuminsured";
                    calculateRollup.Target = new EntityReference("lux_propertyownersapplications", appln.Id);
                    CalculateRollupFieldResponse resp = (CalculateRollupFieldResponse)service.Execute(calculateRollup);
                    Entity QuoteEntity = resp.Entity;
                    decimal TotalSumInsured = ((Money)QuoteEntity.Attributes["lux_totalsuminsured"]).Value;

                    var POL_indemnity = appln.GetAttributeValue<OptionSetValue>("lux_propertyownersliabilitylimitofindemnity");

                    var POLPremium = 0M;
                    var POLTechPrem = 0M;

                    if (POL_indemnity != null)
                    {
                        var TotalBuildingDeclaredFetch = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                                                          <entity name='lux_totalbuildingsdeclaredvaluerate'>
                                                            <attribute name='lux_upto2999999' />
                                                            <attribute name='lux_5mto9999999' />
                                                            <attribute name='lux_3mto4999999' />
                                                            <attribute name='lux_25mplus' />
                                                            <attribute name='lux_10mto24999999' />
                                                            <attribute name='lux_totalbuildingsdeclaredvaluerateid' />
                                                            <attribute name='lux_propertyownersliability' />
                                                            <order attribute='lux_upto2999999' descending='false' />
                                                            <filter type='and'>
                                                              <condition attribute='statecode' operator='eq' value='0' />
                                                              <condition attribute='lux_propertyownersliability' operator='eq' value='{POL_indemnity.Value}' />
                                                            </filter>
                                                          </entity>
                                                        </fetch>";
                        if (service.RetrieveMultiple(new FetchExpression(TotalBuildingDeclaredFetch)).Entities.Count > 0)
                        {
                            var rating = "";
                            var BD_Rate = service.RetrieveMultiple(new FetchExpression(TotalBuildingDeclaredFetch)).Entities[0];
                            if (TotalSumInsured < 2999999)
                            {
                                rating = "lux_upto2999999";
                            }
                            else if (TotalSumInsured >= 3000000 && TotalSumInsured < 4999999)
                            {
                                rating = "lux_3mto4999999";
                            }
                            else if (TotalSumInsured >= 5000000 && TotalSumInsured < 9999999)
                            {
                                rating = "lux_5mto9999999";
                            }
                            else if (TotalSumInsured >= 10000000 && TotalSumInsured <= 25000000)
                            {
                                rating = "lux_10mto24999999";
                            }
                            else
                            {
                                rating = "lux_25mplus";
                            }
                            var BDRate = BD_Rate.GetAttributeValue<decimal>(rating);
                            POLPremium = TotalSumInsured * BDRate / 100;
                            POLTechPrem = POLPremium * dateDiffDays / 365;
                            TotalPOLPremium = POLPremium * dateDiffDays / 365;
                            if (TotalPOLPremium < 25 && ApplicationType != 972970001)
                            {
                                if (appln.GetAttributeValue<OptionSetValue>("lux_applicationtype").Value == 972970003)
                                {
                                    TotalPOLPremium = 25;
                                }
                                else
                                {
                                    if (appln.Attributes.Contains("lux_policy"))
                                    {
                                        var Policy = service.Retrieve("lux_policy", appln.GetAttributeValue<EntityReference>("lux_policy").Id, new ColumnSet("lux_policytype"));
                                        if (Policy.GetAttributeValue<OptionSetValue>("lux_policytype").Value == 972970001)
                                        {
                                            if (TotalPOLPremium < 5)
                                            {
                                                TotalPOLPremium = 5;
                                            }
                                        }
                                        else
                                        {
                                            TotalPOLPremium = 25;
                                        }
                                    }
                                }
                            }
                            else
                            {
                                if (TotalPOLPremium < 5)
                                {
                                    TotalPOLPremium = 5;
                                }
                            }
                            appln["lux_propertyownersliabilitypremium"] = new Money(TotalPOLPremium);
                        }
                        else
                        {
                            TotalPOLPremium = 0;
                            appln["lux_propertyownersliabilitypremium"] = new Money(0);
                        }
                    }
                    else
                    {
                        TotalPOLPremium = 0;
                        appln["lux_propertyownersliabilitypremium"] = new Money(0);
                    }

                    //ARAG Premium
                    var aragfetch = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                                          <entity name='lux_aragrate'>
                                            <attribute name='createdon' />
                                            <attribute name='lux_product' />
                                            <attribute name='lux_netrate' />
                                            <attribute name='lux_grossrate' />                                
                                            <attribute name='lux_turnoverfrom' />
                                            <attribute name='lux_turnoverto' />
                                            <attribute name='lux_aragrateid' />
                                            <order attribute='createdon' descending='false' />
                                            <filter type='and'>
                                              <condition attribute='statecode' operator='eq' value='0' />
                                              <condition attribute='lux_product' operator='eq' uiname='' uitype='product' value='{productData.Id}' />
                                              <condition attribute='lux_startdate' operator='on-or-before' value='{String.Format("{0:MM/dd/yyyy}", inceptionDate)}' />
                                              <filter type='or'>
                                                  <condition attribute='lux_enddate' operator='on-or-after' value='{String.Format("{0:MM/dd/yyyy}", inceptionDate)}' />
                                                  <condition attribute='lux_enddate' operator='null' />
                                              </filter>
                                            </filter>
                                          </entity>
                                        </fetch>";

                    if (service.RetrieveMultiple(new FetchExpression(aragfetch)).Entities.Count > 0)
                    {
                        var Rates = service.RetrieveMultiple(new FetchExpression(aragfetch)).Entities[0];

                        if (inceptionDate >= new DateTime(2025, 05, 01))
                        {
                            if (NoOfLocations == 1)
                            {
                                appln["lux_legrosspremium"] = new Money(35M * dateDiffDays / 365);
                                appln["lux_lenetpremium"] = new Money(17.88M * dateDiffDays / 365);

                                TotalLEGrossPremium = 35M * dateDiffDays / 365;
                                TotalLENetPremium = 17.88M * dateDiffDays / 365;
                            }
                            else
                            {
                                appln["lux_legrosspremium"] = new Money(Rates.GetAttributeValue<Money>("lux_grossrate").Value * dateDiffDays / 365);
                                appln["lux_lenetpremium"] = new Money(Rates.GetAttributeValue<Money>("lux_netrate").Value * dateDiffDays / 365);

                                TotalLEGrossPremium = Rates.GetAttributeValue<Money>("lux_grossrate").Value * dateDiffDays / 365;
                                TotalLENetPremium = Rates.GetAttributeValue<Money>("lux_netrate").Value * dateDiffDays / 365;
                            }
                        }
                        else
                        {
                            var POProuctType = appln.Attributes.Contains("lux_rpocpoproducttype") ? appln.GetAttributeValue<OptionSetValue>("lux_rpocpoproducttype").Value : 0;
                            if (POProuctType == 972970001)
                            {
                                if (NoOfLocations == 1)
                                {
                                    appln["lux_legrosspremium"] = new Money(35M * dateDiffDays / 365);
                                    appln["lux_lenetpremium"] = new Money(14.30M * dateDiffDays / 365);

                                    TotalLEGrossPremium = 35M * dateDiffDays / 365;
                                    TotalLENetPremium = 14.30M * dateDiffDays / 365;
                                }
                                else
                                {
                                    appln["lux_legrosspremium"] = new Money(Rates.GetAttributeValue<Money>("lux_grossrate").Value * dateDiffDays / 365);
                                    appln["lux_lenetpremium"] = new Money(Rates.GetAttributeValue<Money>("lux_netrate").Value * dateDiffDays / 365);

                                    TotalLEGrossPremium = Rates.GetAttributeValue<Money>("lux_grossrate").Value * dateDiffDays / 365;
                                    TotalLENetPremium = Rates.GetAttributeValue<Money>("lux_netrate").Value * dateDiffDays / 365;
                                }
                            }
                            else
                            {
                                appln["lux_legrosspremium"] = new Money(Rates.GetAttributeValue<Money>("lux_grossrate").Value * dateDiffDays / 365);
                                appln["lux_lenetpremium"] = new Money(Rates.GetAttributeValue<Money>("lux_netrate").Value * dateDiffDays / 365);

                                TotalLEGrossPremium = Rates.GetAttributeValue<Money>("lux_grossrate").Value * dateDiffDays / 365;
                                TotalLENetPremium = Rates.GetAttributeValue<Money>("lux_netrate").Value * dateDiffDays / 365;
                            }
                        }
                    }

                    appln["lux_businessinterruptionpremium"] = new Money(TotalBIPremium);
                    appln["lux_materialdamagepremium"] = new Money(TotalMDPremium);

                    //var TotalPremium = TotalMDPremium + TotalBIPremium + TotalELPremium + TotalPOLPremium;
                    //if (TotalPremium < 100)
                    //{
                    //    TotalPremium = 100;
                    //}

                    var TotalPropertyPremium = TotalMDPremium + TotalBIPremium + TotalPOLPremium;
                    var TotalLiabilityPremium = TotalELPremium;
                    var TotalPremium = TotalPropertyPremium + TotalLiabilityPremium;

                    appln["lux_totalpremium"] = new Money(TotalPremium + TotalLEGrossPremium);
                    appln["lux_brokercommission"] = Convert.ToDouble(BrokerComm) + "%";
                    appln["lux_aciestechnicalcommission"] = Convert.ToDouble(aciesComm) + "%";
                    var TotalTechComm = Convert.ToDouble(BrokerComm) + Convert.ToDouble(aciesComm);
                    appln["lux_totaltechnicalcommission"] = TotalTechComm + "%";

                    var LEBrokerComm = TotalLEGrossPremium * BrokerComm / 100;
                    var LeGrossComm = TotalLEGrossPremium - TotalLENetPremium - LEBrokerComm;

                    appln["lux_brokercommissionamount"] = new Money(TotalPremium * BrokerComm / 100 + LEBrokerComm);
                    appln["lux_legrosscommission"] = new Money(LeGrossComm);
                    appln["lux_aciestechnicalcommissionamount"] = new Money(TotalPremium * aciesComm / 100);

                    appln["lux_originaltechnicalpremium"] = new Money(TotalPremium + TotalLENetPremium - TotalPremium * BrokerComm / 100 - TotalPremium * aciesComm / 100);
                    appln["lux_technicalnetpremium"] = new Money(TotalPremium + TotalLENetPremium - TotalPremium * BrokerComm / 100 - TotalPremium * aciesComm / 100);

                    if (inceptionDate >= new DateTime(2023, 11, 01))
                    {
                        appln["lux_aciestechnicalcommissionamount"] = new Money(TotalPropertyPremium * aciesComm / 100 + TotalLiabilityPremium * LiabilityaciesComm / 100);
                        appln["lux_originaltechnicalpremium"] = new Money(TotalPremium + TotalLENetPremium - TotalPremium * BrokerComm / 100 - TotalPropertyPremium * aciesComm / 100 - TotalLiabilityPremium * LiabilityaciesComm / 100);
                        appln["lux_technicalnetpremium"] = new Money(TotalPremium + TotalLENetPremium - TotalPremium * BrokerComm / 100 - TotalPropertyPremium * aciesComm / 100 - TotalLiabilityPremium * LiabilityaciesComm / 100);
                    }

                    decimal commercialLoadDiscount = appln.Attributes.Contains("lux_commercialloaddiscount") ? appln.GetAttributeValue<decimal>("lux_commercialloaddiscount") : -30;
                    commercialLoadDiscount = Broker.Attributes.Contains("lux_discount") ? Broker.GetAttributeValue<decimal>("lux_discount") : commercialLoadDiscount;

                    decimal MDSectionDiscount = appln.Attributes.Contains("lux_materialdamagesectiondiscount") ? appln.GetAttributeValue<decimal>("lux_materialdamagesectiondiscount") : commercialLoadDiscount;
                    decimal BISectionDiscount = appln.Attributes.Contains("lux_businessinterruptionsectionadjustment") ? appln.GetAttributeValue<decimal>("lux_businessinterruptionsectionadjustment") : commercialLoadDiscount;
                    decimal ELSectionDiscount = appln.Attributes.Contains("lux_employersliabilitysectiondiscount") ? appln.GetAttributeValue<decimal>("lux_employersliabilitysectiondiscount") : commercialLoadDiscount;
                    decimal PLSectionDiscount = appln.Attributes.Contains("lux_publicproductsliabilitysectiondiscount") ? appln.GetAttributeValue<decimal>("lux_publicproductsliabilitysectiondiscount") : commercialLoadDiscount;

                    if (!appln.Attributes.Contains("lux_commercialloaddiscount") || appln.GetAttributeValue<decimal>("lux_commercialloaddiscount") == 0)
                    {
                        commercialLoadDiscount = -30;
                        if (IsLive == true)
                        {
                            var LoadDiscountFetch = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                                                  <entity name='lux_lux_quoteandbindloaddiscount'>
                                                    <attribute name='createdon' />
                                                    <attribute name='lux_validuntil' />
                                                    <attribute name='lux_effectivedate' />
                                                    <attribute name='lux_commercialloaddiscount' />
                                                    <attribute name='lux_producttype' />
                                                    <attribute name='lux_product' />
                                                    <attribute name='lux_lux_quoteandbindloaddiscountid' />
                                                    <order attribute='lux_commercialloaddiscount' descending='false' />
                                                    <filter type='and'>
                                                      <condition attribute='statecode' operator='eq' value='0' />
                                                      <condition attribute='lux_product' operator='eq' uiname='' uitype='product' value='{productData.Id}' />
                                                      <condition attribute='lux_effectivedate' operator='on-or-before' value='{String.Format("{0:MM/dd/yyyy}", DateTime.UtcNow)}' />
                                                      <filter type='or'>
                                                        <condition attribute='lux_validuntil' operator='on-or-after' value='{String.Format("{0:MM/dd/yyyy}", DateTime.UtcNow)}' />
                                                        <condition attribute='lux_validuntil' operator='null' />
                                                      </filter>
                                                    </filter>
                                                  </entity>
                                                </fetch>";

                            var loadDiscData = service.RetrieveMultiple(new FetchExpression(LoadDiscountFetch)).Entities;

                            if (loadDiscData.Count > 0)
                            {
                                if (rpoProductType == 972970001 || rpoProductType == 972970003) //Residential
                                {
                                    commercialLoadDiscount = loadDiscData.FirstOrDefault(x => x.GetAttributeValue<OptionSetValue>("lux_producttype").Value == 972970001).GetAttributeValue<decimal>("lux_commercialloaddiscount");
                                }
                                else
                                {
                                    commercialLoadDiscount = loadDiscData.FirstOrDefault(x => x.GetAttributeValue<OptionSetValue>("lux_producttype").Value == 972970002).GetAttributeValue<decimal>("lux_commercialloaddiscount");
                                    if (IsLeisureTrade == true)
                                    {
                                        commercialLoadDiscount = -20;
                                    }
                                }
                            }
                        }
                        MDSectionDiscount = commercialLoadDiscount;
                        BISectionDiscount = commercialLoadDiscount;
                        ELSectionDiscount = commercialLoadDiscount;
                        PLSectionDiscount = commercialLoadDiscount;

                        if (IsStudentHolidayLetTenant == true)
                        {
                            commercialLoadDiscount = -20;
                            MDSectionDiscount = -20;
                            BISectionDiscount = -20;
                            ELSectionDiscount = -20;
                            PLSectionDiscount = -20;
                        }
                    }

                    appln["lux_commercialloaddiscount"] = commercialLoadDiscount;
                    appln["lux_materialdamagesectiondiscount"] = MDSectionDiscount;
                    appln["lux_businessinterruptionsectionadjustment"] = BISectionDiscount;
                    appln["lux_employersliabilitysectiondiscount"] = ELSectionDiscount;
                    appln["lux_publicproductsliabilitysectiondiscount"] = PLSectionDiscount;

                    TotalMDPremium = TotalMDPremium + TotalMDPremium * MDSectionDiscount / 100;
                    if (TotalMDPremium <= 75)
                    {
                        TotalMDPremium = 75;
                    }

                    TotalBIPremium = TotalBIPremium + TotalBIPremium * BISectionDiscount / 100;
                    if (TotalBIPremium > 0 && TotalBIPremium <= 25 && appln.GetAttributeValue<OptionSetValue>("lux_applicationtype").Value != 972970001)
                    {
                        TotalBIPremium = 25;
                    }

                    TotalELPremium = TotalELPremium + TotalELPremium * ELSectionDiscount / 100;
                    if (appln.GetAttributeValue<bool>("lux_iselcoverrequired") == true && TotalELPremium <= 25 && appln.GetAttributeValue<OptionSetValue>("lux_applicationtype").Value != 972970001)
                    {
                        TotalELPremium = 25;
                    }

                    if (appln.GetAttributeValue<bool>("lux_iselcoverrequired") == true && TotalELPremium < 25)
                    {
                        TotalELPremium = 25;
                    }

                    TotalPOLPremium = TotalPOLPremium + TotalPOLPremium * PLSectionDiscount / 100;
                    if (TotalPOLPremium <= 25 && ApplicationType != 972970001)
                    {
                        if (ApplicationType == 972970003)
                        {
                            TotalPOLPremium = 25;
                        }
                        else if (POLTechPrem == 5 && POLPremium <= 5)
                        {
                            TotalPOLPremium = 5;
                        }
                        else if (POLTechPrem == 25 && POLPremium <= 25)
                        {
                            TotalPOLPremium = 25;
                        }
                    }
                    else
                    {
                        if (TotalPOLPremium < 5)
                        {
                            TotalPOLPremium = 5;
                        }
                    }

                    var MDNetPremium = TotalMDPremium - TotalMDPremium * totaltechnicalcommission / 100;
                    var BINetPremium = TotalBIPremium - TotalBIPremium * totaltechnicalcommission / 100;
                    var POLNetPremium = TotalPOLPremium - TotalPOLPremium * totaltechnicalcommission / 100;

                    var MDPolicyPremium = MDNetPremium / (1 - totalpolicycommission / 100);
                    appln["lux_materialdamagepolicypremium"] = new Money(MDPolicyPremium);

                    var BIPolicyPremium = BINetPremium / (1 - totalpolicycommission / 100);
                    appln["lux_businessinterruptionpolicypremium"] = new Money(BIPolicyPremium);

                    var POLPolicyPremium = POLNetPremium / (1 - totalpolicycommission / 100);

                    appln["lux_propertyownersliabilitypolicypremium"] = new Money(POLPolicyPremium);

                    var ELNetPremium = TotalELPremium - TotalELPremium * totaltechnicalcommission / 100;
                    var ELPolicyPremium = ELNetPremium / (1 - totalpolicycommission / 100);
                    appln["lux_employersliabilitypolicypremium"] = new Money(ELPolicyPremium);

                    if (inceptionDate >= new DateTime(2023, 11, 01))
                    {
                        ELNetPremium = TotalELPremium - TotalELPremium * totalLiabilitytechnicalcommission / 100;
                        ELPolicyPremium = ELNetPremium / (1 - totalLiabilitypolicycommission / 100);
                        appln["lux_employersliabilitypolicypremium"] = new Money(ELPolicyPremium);
                    }

                    var TotalPolicyNetPremium = MDNetPremium + BINetPremium + ELNetPremium + POLNetPremium + TotalLENetPremium;
                    appln["lux_policynetpremium"] = new Money(TotalPolicyNetPremium);

                    var TotalPropertyPolicyPremium = MDPolicyPremium + BIPolicyPremium + POLPolicyPremium;
                    var TotalLiabilityPolicyPremium = ELPolicyPremium;
                    var TotalPolicyPremium = TotalPropertyPolicyPremium + TotalLiabilityPolicyPremium;

                    appln["lux_policybrokercommission"] = Convert.ToDouble(PolicyBrokerComm) + "%";
                    appln["lux_policyaciescommission"] = Convert.ToDouble(PolicyaciesComm) + "%";
                    var TotalPolComm = Convert.ToDouble(PolicyBrokerComm) + Convert.ToDouble(PolicyaciesComm);
                    appln["lux_policytotalcommission"] = TotalPolComm + "%";

                    appln["lux_lepolicygrosspremium"] = new Money(TotalLEGrossPremium);
                    appln["lux_lepolicynetpremium"] = new Money(TotalLENetPremium);

                    var LEPolicyBrokerComm = TotalLEGrossPremium * PolicyBrokerComm / 100;
                    var LePolicyGrossComm = TotalLEGrossPremium - TotalLENetPremium - LEPolicyBrokerComm;

                    appln["lux_quotedpremiumbrokercommissionamount"] = new Money(TotalPolicyPremium * PolicyBrokerComm / 100 + LEPolicyBrokerComm);
                    appln["lux_lepolicygrosscommission"] = new Money(LePolicyGrossComm);
                    appln["lux_quotedpremiumaciescommissionamount"] = new Money(TotalPolicyPremium * PolicyaciesComm / 100);

                    if (inceptionDate >= new DateTime(2023, 11, 01))
                    {
                        appln["lux_quotedpremiumbrokercommissionamount"] = new Money(TotalPolicyPremium * PolicyBrokerComm / 100 + LEPolicyBrokerComm);
                        appln["lux_quotedpremiumaciescommissionamount"] = new Money(TotalPropertyPolicyPremium * PolicyaciesComm / 100 + TotalLiabilityPolicyPremium * PolicyLiabilityaciesComm / 100);
                        appln["lux_quotedpremiumaciescommissionamountliabili"] = new Money(TotalLiabilityPolicyPremium * PolicyLiabilityaciesComm / 100);
                        appln["lux_lepolicygrosscommission"] = new Money(LePolicyGrossComm);
                        appln["lux_policyaciescommissionliability"] = Convert.ToDouble(PolicyLiabilityaciesComm) + "%";
                    }

                    appln["statuscode"] = new OptionSetValue(972970003);

                    decimal Fee = 0;
                    decimal PolicyFee = 0;

                    var FeeFetch = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                                      <entity name='lux_adminfeerule'>
                                        <attribute name='lux_to' />
                                        <attribute name='lux_from' />
                                        <attribute name='lux_fee' />
                                        <attribute name='lux_adminfeeruleid' />
                                        <order attribute='lux_to' descending='false' />
                                        <filter type='and'>
                                          <condition attribute='statecode' operator='eq' value='0' />
                                          <condition attribute='lux_from' operator='le' value='{TotalPremium + TotalLEGrossPremium}' />
                                          <filter type='or'>
                                            <condition attribute='lux_to' operator='ge' value='{TotalPremium + TotalLEGrossPremium}' />
                                            <condition attribute='lux_to' operator='null' />
                                          </filter>
                                        </filter>
                                      </entity>
                                    </fetch>";
                    if (service.RetrieveMultiple(new FetchExpression(FeeFetch)).Entities.Count > 0)
                    {
                        Fee = service.RetrieveMultiple(new FetchExpression(FeeFetch)).Entities[0].GetAttributeValue<Money>("lux_fee").Value;
                        if (TotalPremium + TotalLEGrossPremium >= 5000 && TotalPremium + TotalLEGrossPremium < 10000)
                        {
                            Fee = 75;
                        }
                        else if (TotalPremium + TotalLEGrossPremium >= 10000 && TotalPremium + TotalLEGrossPremium < 25000)
                        {
                            Fee = 100;
                        }
                        else if (TotalPremium + TotalLEGrossPremium >= 25000 && TotalPremium + TotalLEGrossPremium < 50000)
                        {
                            Fee = 150;
                        }
                        else if (TotalPremium + TotalLEGrossPremium >= 50000)
                        {
                            Fee = 200;
                        }
                    }
                    var PolicyFeeFetch = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                                      <entity name='lux_adminfeerule'>
                                        <attribute name='lux_to' />
                                        <attribute name='lux_from' />
                                        <attribute name='lux_fee' />
                                        <attribute name='lux_adminfeeruleid' />
                                        <order attribute='lux_to' descending='false' />
                                        <filter type='and'>
                                          <condition attribute='statecode' operator='eq' value='0' />
                                          <condition attribute='lux_from' operator='le' value='{TotalPolicyPremium + TotalLEGrossPremium}' />
                                          <filter type='or'>
                                            <condition attribute='lux_to' operator='ge' value='{TotalPolicyPremium + TotalLEGrossPremium}' />
                                            <condition attribute='lux_to' operator='null' />
                                          </filter>
                                        </filter>
                                      </entity>
                                    </fetch>";
                    if (service.RetrieveMultiple(new FetchExpression(PolicyFeeFetch)).Entities.Count > 0)
                    {
                        PolicyFee = service.RetrieveMultiple(new FetchExpression(PolicyFeeFetch)).Entities[0].GetAttributeValue<Money>("lux_fee").Value;
                        if (inceptionDate >= new DateTime(2024, 02, 01))
                        {
                            if (TotalPolicyPremium + TotalLEGrossPremium >= 5000 && TotalPolicyPremium + TotalLEGrossPremium < 10000)
                            {
                                PolicyFee = 75;
                            }
                            else if (TotalPolicyPremium + TotalLEGrossPremium >= 10000 && TotalPolicyPremium + TotalLEGrossPremium < 25000)
                            {
                                PolicyFee = 100;
                            }
                            else if (TotalPolicyPremium + TotalLEGrossPremium >= 25000 && TotalPolicyPremium + TotalLEGrossPremium < 50000)
                            {
                                PolicyFee = 150;
                            }
                            else if (TotalPolicyPremium + TotalLEGrossPremium >= 50000)
                            {
                                PolicyFee = 200;
                            }
                        }
                    }
                    appln["lux_fees"] = new Money(Fee);
                    appln["lux_policyfee"] = new Money(PolicyFee);
                    appln["lux_quotetype"] = true;
                    appln["lux_quotationdate"] = DateTime.UtcNow;
                    service.Update(appln);
                }
                return "Success";
            }
            catch (Exception ex)
            {
                return "Failure";
            }
        }


        public static string CalculateOtherProductsPremium(Entity appln, IOrganizationService service, bool IsLive)
        {
            try
            {
                var dateDiffDays = (appln.GetAttributeValue<DateTime>("lux_renewaldate") - appln.GetAttributeValue<DateTime>("lux_inceptiondate")).Days;
                if (dateDiffDays == 363 || dateDiffDays == 364 || dateDiffDays == 365 || dateDiffDays == 366 || dateDiffDays == 367)
                {
                    dateDiffDays = 365;
                }
                var quotationDate = appln.Contains("lux_quotationdate") ? appln.GetAttributeValue<DateTime>("lux_quotationdate") : appln.GetAttributeValue<DateTime>("lux_inceptiondate");
                var inceptionDate = Convert.ToDateTime(appln.FormattedValues["lux_inceptiondate"], System.Globalization.CultureInfo.GetCultureInfo("en-GB").DateTimeFormat);

                var Broker = service.Retrieve("account", appln.GetAttributeValue<EntityReference>("lux_broker").Id, new ColumnSet(true));
                var productData = service.Retrieve("product", appln.GetAttributeValue<EntityReference>("lux_insuranceproductrequired").Id, new ColumnSet("name"));
                var productName = productData.Attributes["name"].ToString();

                var fetch = "";
                var entityName = "";
                if (productName == "Retail")
                {
                    entityName = "lux_propertyownersretail";
                    fetch = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                              <entity name='lux_propertyownersretail'>
                                <attribute name='lux_propertyownersretailid' />
                                <attribute name='lux_name' />
                                <attribute name='createdon' />
                                <attribute name='lux_generalcontentsdeclaredvalueincludingmach' />
                                <attribute name='lux_contentssuminsuredwithupliftedamount' />
                                <attribute name='lux_buildingsdeclaredvalue' />
                                <attribute name='lux_buildingssuminsuredwithupliftedamount' />
                                <attribute name='lux_listhighvaluestock' />
                                <attribute name='lux_stockexcludinghighvaluestock' />
                                <attribute name='lux_computerandelectronicbusinessequipment' />
                                <attribute name='lux_winesfortifiedwinesspiritsfinesuminsured' />
                                <attribute name='lux_materialdamagecoverdetails' />
                                <attribute name='lux_powertoolssuminsured' />                                
                                <attribute name='lux_tenantsimprovementsdeclaredvalue' />
                                <attribute name='lux_tenantssuminsuredwithupliftedamount' />
                                <attribute name='lux_nonferrousmetalssuminsured' />
                                <attribute name='lux_totalmdsuminsuredwithupliftedamount' />
                                <attribute name='lux_mobilephonessuminsured' />
                                <attribute name='lux_jewellerywatchessuminsured' />
                                <attribute name='lux_computerequipmentsuminsured' />
                                <attribute name='lux_audiovideoequipmentsuminsured' />
                                <attribute name='lux_isdayoneupliftcoverrequired' />                                                  
                                <attribute name='lux_riskpostcode' />                      
                                <attribute name='lux_dayoneupliftcover' />
                                <attribute name='lux_alcoholsuminsured' />
                                <attribute name='lux_computergamesandorconsolessuminsured' />
                                <attribute name='lux_materialdamagelossofrentpayable' />
                                <attribute name='lux_cigarettescigarsortobaccoproductssuminsur' />
                                <attribute name='lux_jewellerywatchessuminsured' />
                                <attribute name='lux_fineartsuminsured' />
                                <order attribute='lux_name' descending='false' />
                                <filter type='and'>
                                  <condition attribute='statecode' operator='eq' value='0' />
                                  <condition attribute='lux_propertyownersapplications' operator='eq' uiname='' uitype='lux_propertyownersapplications' value='{appln.Id}' />
                                </filter>
                                <link-entity name='lux_propertyownersapplications' from='lux_propertyownersapplicationsid' to='lux_propertyownersapplications' visible='false' link-type='outer' alias='appln'>
                                  <attribute name='lux_maintradeforthispremises' />
                                </link-entity>
                              </entity>
                            </fetch>";
                }
                else if (productName == "Commercial Combined")
                {
                    entityName = "lux_commercialcombinedapplication";
                    fetch = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                              <entity name='lux_commercialcombinedapplication'>
                                <attribute name='lux_commercialcombinedapplicationid' />
                                <attribute name='lux_name' />
                                <attribute name='createdon' />
                                <attribute name='lux_isdayoneupliftcoverrequired' />                                
                                <attribute name='lux_dayoneupliftcover' />
                                <attribute name='lux_generalcontentsdeclaredvalueincludingmach' />
                                <attribute name='lux_contentssuminsuredwithupliftedamount' />
                                <attribute name='lux_buildingsdeclaredvalue' />
                                <attribute name='lux_totalmdsuminsuredwithupliftedamount' />
                                <attribute name='lux_buildingssuminsuredwithupliftedamount' />
                                <attribute name='lux_listhighvaluestock' />
                                <attribute name='lux_stockexcludinghighvaluestock' />
                                <attribute name='lux_computerandelectronicbusinessequipment' />
                                <attribute name='lux_winesfortifiedwinesspiritsfinesuminsured' />
                                <attribute name='lux_materialdamagecoverdetails' />
                                <attribute name='lux_powertoolssuminsured' />    
                                <attribute name='lux_riskpostcode' />  
                                <attribute name='lux_tenantsimprovementsdeclaredvalue' />
                                <attribute name='lux_tenantssuminsuredwithupliftedamount' />
                                <attribute name='lux_nonferrousmetalssuminsured' />
                                <attribute name='lux_mobilephonessuminsured' />
                                <attribute name='lux_jewellerywatchessuminsured' />
                                <attribute name='lux_computerequipmentsuminsured' />
                                <attribute name='lux_audiovideoequipmentsuminsured' />
                                <attribute name='lux_alcoholsuminsured' />
                                <attribute name='lux_computergamesandorconsolessuminsured' />
                                <attribute name='lux_materialdamagelossofrentpayable' />
                                <attribute name='lux_cigarettescigarsortobaccoproductssuminsur' />
                                <attribute name='lux_jewellerywatchessuminsured' />
                                <attribute name='lux_fineartsuminsured' />
                                <order attribute='lux_name' descending='false' />
                                <filter type='and'>
                                  <condition attribute='statecode' operator='eq' value='0' />
                                  <condition attribute='lux_propertyownersapplications' operator='eq' uiname='' uitype='lux_propertyownersapplications' value='{appln.Id}' />
                                </filter>
                                <link-entity name='lux_propertyownersapplications' from='lux_propertyownersapplicationsid' to='lux_propertyownersapplications' visible='false' link-type='outer' alias='appln'>
                                  <attribute name='lux_maintradeforthispremises' />
                                </link-entity>
                              </entity>
                            </fetch>";
                }
                else if (productName == "Pubs & Restaurants" || productName == "Hotels and Guesthouses")
                {
                    entityName = "lux_pubsrestaurantspropertyownersapplicatio";
                    fetch = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                              <entity name='lux_pubsrestaurantspropertyownersapplicatio'>
                                <attribute name='lux_pubsrestaurantspropertyownersapplicatioid' />
                                <attribute name='lux_name' />
                                <attribute name='createdon' />
                                <attribute name='lux_isdayoneupliftcoverrequired' />                                
                                <attribute name='lux_dayoneupliftcover' />
                                <attribute name='lux_generalcontentsdeclaredvalueincludingmach' />
                                <attribute name='lux_totalmdsuminsuredwithupliftedamount' />
                                <attribute name='lux_contentssuminsuredwithupliftedamount' />
                                <attribute name='lux_buildingsdeclaredvalue' />
                                <attribute name='lux_riskpostcode' />  
                                <attribute name='lux_buildingssuminsuredwithupliftedamount' />
                                <attribute name='lux_listhighvaluestock' />
                                <attribute name='lux_stockexcludinghighvaluestock' />
                                <attribute name='lux_computerandelectronicbusinessequipment' />
                                <attribute name='lux_winesfortifiedwinesspiritsfinesuminsured' />
                                <attribute name='lux_materialdamagecoverdetails' />
                                <attribute name='lux_powertoolssuminsured' />                                
                                <attribute name='lux_tenantsimprovementsdeclaredvalue' />
                                <attribute name='lux_tenantssuminsuredwithupliftedamount' />
                                <attribute name='lux_nonferrousmetalssuminsured' />
                                <attribute name='lux_mobilephonessuminsured' />
                                <attribute name='lux_jewellerywatchessuminsured' />
                                <attribute name='lux_computerequipmentsuminsured' />
                                <attribute name='lux_audiovideoequipmentsuminsured' />
                                <attribute name='lux_alcoholsuminsured' />
                                <attribute name='lux_computergamesandorconsolessuminsured' />
                                <attribute name='lux_materialdamagelossofrentpayable' />
                                <attribute name='lux_cigarettescigarsortobaccoproductssuminsur' />
                                <attribute name='lux_jewellerywatchessuminsured' />
                                <attribute name='lux_fineartsuminsured' />
                                <order attribute='lux_name' descending='false' />
                                <filter type='and'>
                                  <condition attribute='statecode' operator='eq' value='0' />
                                  <condition attribute='lux_propertyownersapplications' operator='eq' uiname='' uitype='lux_propertyownersapplications' value='{appln.Id}' />
                                </filter>
                                <link-entity name='lux_propertyownersapplications' from='lux_propertyownersapplicationsid' to='lux_propertyownersapplications' visible='false' link-type='outer' alias='appln'>
                                  <attribute name='lux_maintradeforthispremises' />
                                </link-entity>
                              </entity>
                            </fetch>";
                }
                else if (productName == "Contractors Combined")
                {
                    entityName = "lux_contractorscombined";
                    fetch = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                              <entity name='lux_contractorscombined'>
                                <attribute name='lux_contractorscombinedid' />
                                <attribute name='lux_name' />
                                <attribute name='createdon' />
                                <attribute name='lux_isdayoneupliftcoverrequired' />                                
                                <attribute name='lux_dayoneupliftcover' />
                                <attribute name='lux_generalcontentsdeclaredvalueincludingmach' />
                                <attribute name='lux_contentssuminsuredwithupliftedamount' />
                                <attribute name='lux_buildingsdeclaredvalue' />
                                <attribute name='lux_buildingssuminsuredwithupliftedamount' />
                                <attribute name='lux_listhighvaluestock' />
                                <attribute name='lux_stockexcludinghighvaluestock' />
                                <attribute name='lux_computerandelectronicbusinessequipment' />
                                <attribute name='lux_winesfortifiedwinesspiritsfinesuminsured' />
                                <attribute name='lux_totalmdsuminsuredwithupliftedamount' />
                                <attribute name='lux_materialdamagecoverdetails' />
                                <attribute name='lux_powertoolssuminsured' />  
                                <attribute name='lux_riskpostcode' />  
                                <attribute name='lux_tenantsimprovementsdeclaredvalue' />
                                <attribute name='lux_tenantssuminsuredwithupliftedamount' />
                                <attribute name='lux_nonferrousmetalssuminsured' />
                                <attribute name='lux_mobilephonessuminsured' />
                                <attribute name='lux_jewellerywatchessuminsured' />
                                <attribute name='lux_computerequipmentsuminsured' />
                                <attribute name='lux_audiovideoequipmentsuminsured' />
                                <attribute name='lux_alcoholsuminsured' />
                                <attribute name='lux_computergamesandorconsolessuminsured' />
                                <attribute name='lux_materialdamagelossofrentpayable' />
                                <attribute name='lux_cigarettescigarsortobaccoproductssuminsur' />
                                <attribute name='lux_jewellerywatchessuminsured' />
                                <attribute name='lux_fineartsuminsured' />
                                <order attribute='lux_name' descending='false' />
                                <filter type='and'>
                                  <condition attribute='statecode' operator='eq' value='0' />
                                  <condition attribute='lux_propertyownersapplications' operator='eq' uiname='' uitype='lux_propertyownersapplications' value='{appln.Id}' />
                                </filter>
                                <link-entity name='lux_propertyownersapplications' from='lux_propertyownersapplicationsid' to='lux_propertyownersapplications' visible='false' link-type='outer' alias='appln'>
                                  <attribute name='lux_contractorsprimarytrade' />
                                  <attribute name='lux_ismaterialdamagecoverrequired' />
                                </link-entity>
                              </entity>
                            </fetch>";
                }

                var premises = service.RetrieveMultiple(new FetchExpression(fetch)).Entities;
                var PremiseCount = premises.Count;

                decimal BuildingPremium = 0;
                decimal ContentsPremium = 0;
                decimal TenentsPremium = 0;
                decimal StockPremium = 0;
                decimal TargetStockPremium = 0;
                decimal ComputerEquipmentPremium = 0;
                decimal LossofRentPremium = 0;

                decimal TotalBuildingPremium = 0;
                decimal TotalContentsPremium = 0;
                decimal TotalTenentsPremium = 0;
                decimal TotalStockPremium = 0;
                decimal TotalTargetStockPremium = 0;
                decimal TotalComputerEquipmentPremium = 0;
                decimal TotalLossofRentPremium = 0;

                decimal TotalBuildingFireRate = 0;
                decimal TotalBuildingPerilsRate = 0;
                decimal TotalContentsFireRate = 0;
                decimal TotalContentsTheftRate = 0;
                decimal TotalContentsPerilsRate = 0;
                decimal TotalStockRate = 0;
                decimal TotalTargetStockRate = 0;
                decimal TotalComputerEquipmentRate = 0;
                decimal TotalLORRate = 0;

                decimal TotalBISumInsured = 0;
                decimal LORAmount = 0;

                decimal TotalGrossProfitRevenuePremium = 0;
                decimal TotalGrossProfitRevenueRate = 0;
                decimal TotalIncreasedICOWPremium = 0;
                decimal TotalIncreasedICOWRate = 0;
                decimal TotalAdditionalIncreasedICOWPremium = 0;
                decimal TotalAdditionalIncreasedICOWRate = 0;
                decimal TotalLORPremium = 0;

                decimal TotalMDPremium = 0;
                decimal TotalBIPremium = 0;
                decimal TotalMoneyPremium = 0;
                decimal TotalAllRiskPremium = 0;
                decimal TotalGITPremium = 0;
                decimal TotalELPremium = 0;
                decimal TotalPLPremium = 0;
                decimal TotalCARPremium = 0;
                decimal TotalLENetPremium = 0;
                decimal TotalLEGrossPremium = 0;

                decimal BrokerComm = 25;
                decimal aciesComm = 10;
                decimal LiabilityaciesComm = 5;

                var BrokerFetch = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                                          <entity name='lux_brokercommission'>
                                            <attribute name='createdon' />
                                            <attribute name='lux_product' />
                                            <attribute name='lux_commission' />
                                            <attribute name='lux_brokercommissionid' />
                                            <order attribute='createdon' descending='false' />
                                            <filter type='and'>
                                              <condition attribute='statecode' operator='eq' value='0' />
                                              <filter type='or'>
                                                <condition attribute='lux_effectivefrom' operator='on-or-before' value='{String.Format("{0:MM/dd/yyyy}", inceptionDate)}' />
                                                <condition attribute='lux_effectivefrom' operator='null' />
                                              </filter>
                                              <filter type='or'>
                                                <condition attribute='lux_effectiveto' operator='on-or-after' value= '{String.Format("{0:MM/dd/yyyy}", inceptionDate)}' />
                                                <condition attribute='lux_effectiveto' operator='null' />
                                              </filter>
                                              <condition attribute='lux_broker' operator='eq' uiname='' uitype='account' value='{Broker.Id}' />
                                              <condition attribute='lux_product' operator='eq' uiname='' uitype='product' value='{productData.Id}' />
                                            </filter>
                                          </entity>
                                        </fetch>";
                if (service.RetrieveMultiple(new FetchExpression(BrokerFetch)).Entities.Count > 0)
                {
                    BrokerComm = service.RetrieveMultiple(new FetchExpression(BrokerFetch)).Entities[0].GetAttributeValue<decimal>("lux_commission");
                    aciesComm = 35 - BrokerComm;
                    LiabilityaciesComm = 30 - BrokerComm;
                }
                decimal totaltechnicalcommission = BrokerComm + aciesComm;
                decimal totalLiabilitytechnicalcommission = BrokerComm + LiabilityaciesComm;

                decimal PolicyBrokerComm = appln.Contains("lux_policybrokercommission") ? Convert.ToDecimal(appln.Attributes["lux_policybrokercommission"].ToString().Replace("%", "")) : BrokerComm;
                decimal PolicyaciesComm = appln.Contains("lux_policyaciescommission") ? Convert.ToDecimal(appln.Attributes["lux_policyaciescommission"].ToString().Replace("%", "")) : aciesComm;
                decimal PolicyLiabilityaciesComm = PolicyBrokerComm + PolicyaciesComm <= 30 ? PolicyaciesComm : 30 - PolicyBrokerComm;

                decimal totalpolicycommission = PolicyBrokerComm + PolicyaciesComm;
                decimal totalLiabilitypolicycommission = PolicyBrokerComm + PolicyLiabilityaciesComm;

                var FireRateFetch = "";
                var TradeName = "";
                if (productName != "Contractors Combined")
                {
                    FireRateFetch = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                                              <entity name='lux_propertyownersrate'>
                                                <attribute name='lux_workaway' />
                                                <attribute name='lux_transitratesendings' />
                                                <attribute name='lux_transitrateownvehicle' />
                                                <attribute name='lux_tradesegment' />
                                                <attribute name='lux_tradesector' />
                                                <attribute name='lux_theftstockrate' />
                                                <attribute name='lux_theftcontentsrate' />
                                                <attribute name='lux_theftbyemployeetradebaserate' />
                                                <attribute name='lux_theft' />
                                                <attribute name='lux_productsrate' />
                                                <attribute name='lux_prods' />
                                                <attribute name='lux_plworkawaywagesrate' />
                                                <attribute name='lux_plpremiserate' />
                                                <attribute name='lux_mdbi' />
                                                <attribute name='lux_mdfirerate' />
                                                <attribute name='lux_fulldescription' />
                                                <attribute name='lux_elrate' />
                                                <attribute name='lux_blfirerate' />
                                                <attribute name='lux_el' />
                                                <attribute name='lux_propertyownersrateid' />
                                                <order attribute='lux_blfirerate' descending='false' />
                                                <filter type='and'>
                                                  <condition attribute='statecode' operator='eq' value='0' />
                                                  <condition attribute='lux_startdate' operator='on-or-before' value='{String.Format("{0:MM/dd/yyyy}", quotationDate)}' />
                                                  <filter type='or'>
                                                    <condition attribute='lux_enddate' operator='on-or-after' value='{String.Format("{0:MM/dd/yyyy}", quotationDate)}' />
                                                    <condition attribute='lux_enddate' operator='null' />
                                                  </filter>
                                                  <condition attribute='lux_name' operator='eq' uiname='' value='{appln.FormattedValues["lux_maintradeforthispremises"].ToString()}' />
                                                </filter>
                                              </entity>
                                            </fetch>";
                }
                else
                {
                    FireRateFetch = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                                                  <entity name='lux_contractorstrade'>
                                                    <attribute name='lux_contractorstradeid' />
                                                    <attribute name='lux_name' />
                                                    <attribute name='createdon' />
                                                    <attribute name='lux_tradecategory' />
                                                    <attribute name='lux_plrate' />
                                                    <attribute name='lux_materialdamagefirerate' />
                                                    <attribute name='lux_elrate' />
                                                    <attribute name='lux_carrate' />
                                                    <attribute name='lux_businessinterruptionrate' />
                                                    <order attribute='lux_name' descending='false' />
                                                    <filter type='and'>
                                                      <condition attribute='statecode' operator='eq' value='0' />
                                                      <condition attribute='lux_startdate' operator='on-or-before' value='{String.Format("{0:MM/dd/yyyy}", quotationDate)}' />
                                                      <filter type='or'>
                                                        <condition attribute='lux_enddate' operator='on-or-after' value='{String.Format("{0:MM/dd/yyyy}", quotationDate)}' />
                                                        <condition attribute='lux_enddate' operator='null' />
                                                      </filter>
                                                      <condition attribute='lux_contractorstradeid' operator='eq' uiname='' uitype='lux_contractorstrade' value='{appln.GetAttributeValue<EntityReference>("lux_contractorsprimarytrade").Id}' />
                                                    </filter>
                                                  </entity>
                                                </fetch>";
                }

                foreach (var item in service.RetrieveMultiple(new FetchExpression(fetch)).Entities)
                {
                    var premise_data = item;
                    decimal StockRate = 0;
                    decimal BuildingFireRate = 0;
                    decimal BuildingPerilsRate = 0;
                    decimal ContentsFireRate = 0;
                    decimal ContentsPerilsRate = 0;
                    decimal LORRate = 0;

                    var Buildings = premise_data.Attributes.Contains("lux_buildingsdeclaredvalue") ? premise_data.GetAttributeValue<Money>("lux_buildingsdeclaredvalue").Value : 0;
                    var Tenents = premise_data.Attributes.Contains("lux_tenantsimprovementsdeclaredvalue") ? premise_data.GetAttributeValue<Money>("lux_tenantsimprovementsdeclaredvalue").Value : 0;

                    var Contents = premise_data.Attributes.Contains("lux_generalcontentsdeclaredvalueincludingmach") ? premise_data.GetAttributeValue<Money>("lux_generalcontentsdeclaredvalueincludingmach").Value : 0;
                    var ComputerEquipment = premise_data.Attributes.Contains("lux_computerandelectronicbusinessequipment") ? premise_data.GetAttributeValue<Money>("lux_computerandelectronicbusinessequipment").Value : 0;

                    var covers = premise_data.GetAttributeValue<OptionSetValueCollection>("lux_materialdamagecoverdetails");
                    if (productName != "Contractors Combined")
                    {
                        if (service.RetrieveMultiple(new FetchExpression(FireRateFetch)).Entities.Count > 0)
                        {
                            TradeName = appln.FormattedValues["lux_maintradeforthispremises"].ToString();
                            var FireData = service.RetrieveMultiple(new FetchExpression(FireRateFetch)).Entities[0];
                            if (productName == "Pubs & Restaurants" || productName == "Hotels and Guesthouses")
                            {
                                StockRate = 0.3M;
                            }
                            else if (productName == "Retail")
                            {
                                StockRate = 0.3M;
                            }
                            else if (productName == "Commercial Combined")
                            {
                                if (FireData.FormattedValues["lux_tradesector"].ToString().Contains("Leisure"))
                                {
                                    StockRate = 0.3M;
                                }
                                else if (FireData.FormattedValues["lux_tradesector"].ToString().Contains("Manufacturer's") && appln.FormattedValues["lux_maintradeforthispremises"].ToString() != "Fabric Manufacturing")
                                {
                                    StockRate = 0.3M;
                                }
                                else if (appln.FormattedValues["lux_maintradeforthispremises"].ToString() == "Fabric Manufacturing")
                                {
                                    StockRate = 0.5M;
                                }
                                else if (FireData.FormattedValues["lux_tradesector"].ToString().Contains("Wholesaler's") && appln.FormattedValues["lux_maintradeforthispremises"].ToString() != "Furnishing Fabric Wholesalers" && appln.FormattedValues["lux_maintradeforthispremises"].ToString() != "Mineral Wholesale")
                                {
                                    StockRate = 0.3M;
                                }
                                else if (appln.FormattedValues["lux_maintradeforthispremises"].ToString() == "Furnishing Fabric Wholesalers")
                                {
                                    StockRate = 0.5M;
                                }
                                else if (appln.FormattedValues["lux_maintradeforthispremises"].ToString() == "Mineral Wholesale")
                                {
                                    StockRate = 0.5M;
                                }
                                else
                                {
                                    StockRate = FireData.GetAttributeValue<decimal>("lux_theftstockrate");
                                }
                            }
                            else
                            {
                                StockRate = FireData.GetAttributeValue<decimal>("lux_theftstockrate");
                            }

                            BuildingFireRate = FireData.GetAttributeValue<decimal>("lux_mdfirerate");
                            ContentsFireRate = FireData.GetAttributeValue<decimal>("lux_mdfirerate");
                            LORRate = FireData.GetAttributeValue<decimal>("lux_mdfirerate");
                            TotalContentsTheftRate = FireData.GetAttributeValue<decimal>("lux_theftcontentsrate");

                            if (BuildingFireRate < 0.10M)
                            {
                                BuildingFireRate = 0.10M;
                                ContentsFireRate = 0.10M;
                                LORRate = 0.10M;
                            }

                            if (productName == "Pubs & Restaurants" || productName == "Hotels and Guesthouses")
                            {
                                if (appln.GetAttributeValue<OptionSetValue>("lux_pubsrestaurantproducttype").Value == 972970002)
                                {
                                    BuildingFireRate = 0.14M;
                                    ContentsFireRate = 0.14M;
                                    LORRate = 0.14M;
                                }
                            }
                            if (productName == "Commercial Combined")
                            {
                                if (FireData.FormattedValues["lux_tradesector"].ToString().Contains("Wholesaler's") || FireData.FormattedValues["lux_tradesector"].ToString().Contains("Business") || FireData.FormattedValues["lux_tradesector"].ToString().Contains("Leisure"))
                                {
                                    if (FireData.GetAttributeValue<int>("lux_mdbi") == 2)
                                    {
                                        BuildingFireRate = 0.10M;
                                        ContentsFireRate = 0.10M;
                                        LORRate = 0.10M;
                                    }
                                    else if (FireData.GetAttributeValue<int>("lux_mdbi") == 3)
                                    {
                                        BuildingFireRate = 0.125M;
                                        ContentsFireRate = 0.125M;
                                        LORRate = 0.125M;
                                    }
                                    else if (FireData.GetAttributeValue<int>("lux_mdbi") == 4)
                                    {
                                        BuildingFireRate = 0.175M;
                                        ContentsFireRate = 0.175M;
                                        LORRate = 0.175M;
                                    }
                                    else if (FireData.GetAttributeValue<int>("lux_mdbi") == 5)
                                    {
                                        BuildingFireRate = 0.225M;
                                        ContentsFireRate = 0.225M;
                                        LORRate = 0.225M;
                                    }
                                    else if (FireData.GetAttributeValue<int>("lux_mdbi") == 6)
                                    {
                                        BuildingFireRate = 0.00M;
                                        ContentsFireRate = 0.00M;
                                        LORRate = 0.00M;
                                    }
                                }

                                if (FireData.FormattedValues["lux_tradesector"].ToString().Contains("Manufacturer's"))
                                {
                                    if (FireData.GetAttributeValue<int>("lux_mdbi") == 2)
                                    {
                                        BuildingFireRate = 0.10M;
                                        ContentsFireRate = 0.10M;
                                        LORRate = 0.10M;
                                    }
                                    else if (FireData.GetAttributeValue<int>("lux_mdbi") == 3)
                                    {
                                        BuildingFireRate = 0.15M;
                                        ContentsFireRate = 0.15M;
                                        LORRate = 0.15M;
                                    }
                                    else if (FireData.GetAttributeValue<int>("lux_mdbi") == 4)
                                    {
                                        BuildingFireRate = 0.20M;
                                        ContentsFireRate = 0.20M;
                                        LORRate = 0.20M;
                                    }
                                    else if (FireData.GetAttributeValue<int>("lux_mdbi") == 5)
                                    {
                                        BuildingFireRate = 0.25M;
                                        ContentsFireRate = 0.25M;
                                        LORRate = 0.25M;
                                    }
                                    else if (FireData.GetAttributeValue<int>("lux_mdbi") == 6)
                                    {
                                        BuildingFireRate = 0.00M;
                                        ContentsFireRate = 0.00M;
                                        LORRate = 0.00M;
                                    }
                                }
                            }

                            if (appln.FormattedValues["lux_maintradeforthispremises"].ToString() == "Offices")
                            {
                                BuildingFireRate = 0.09M;
                                ContentsFireRate = 0.09M;
                                LORRate = 0.09M;
                            }
                        }
                    }
                    else if (productName == "Contractors Combined")
                    {
                        if (service.RetrieveMultiple(new FetchExpression(FireRateFetch)).Entities.Count > 0)
                        {
                            var FireData = service.RetrieveMultiple(new FetchExpression(FireRateFetch)).Entities[0];
                            StockRate = Convert.ToDecimal(0.4);
                            BuildingFireRate = FireData.GetAttributeValue<decimal>("lux_materialdamagefirerate");
                            ContentsFireRate = FireData.GetAttributeValue<decimal>("lux_materialdamagefirerate");
                            LORRate = FireData.GetAttributeValue<decimal>("lux_materialdamagefirerate");
                        }
                    }
                    if (covers != null)
                    {
                        var TotalSumInsuredFetch = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                                                  <entity name='lux_totalsuminsuredrate'>
                                                    <attribute name='lux_5m25m' />
                                                    <attribute name='lux_50m100m' />
                                                    <attribute name='lux_25m50m' />
                                                    <attribute name='lux_100m200m' />
                                                    <attribute name='lux_05m' />
                                                    <attribute name='lux_totalsuminsuredrateid' />
                                                    <attribute name='lux_peril' />
                                                    <order attribute='lux_05m' descending='false' />
                                                    <filter type='and'>
                                                      <condition attribute='statecode' operator='eq' value='0' />
                                                      <condition attribute='lux_startdate' operator='on-or-before' value='{String.Format("{0:MM/dd/yyyy}", quotationDate)}' />
                                                      <filter type='or'>
                                                        <condition attribute='lux_enddate' operator='on-or-after' value='{String.Format("{0:MM/dd/yyyy}", quotationDate)}' />
                                                        <condition attribute='lux_enddate' operator='null' />
                                                      </filter>";
                        if (covers != null)
                        {
                            TotalSumInsuredFetch += $@"<condition attribute='lux_peril' operator='contain-values'>";
                            foreach (var cover in covers)
                            {
                                TotalSumInsuredFetch += $@"<value>" + cover.Value + "</value>";
                            }
                            TotalSumInsuredFetch += $@"</condition>";
                        }
                        TotalSumInsuredFetch += $@"</filter>
                                                  </entity>
                                                </fetch>";

                        if (service.RetrieveMultiple(new FetchExpression(TotalSumInsuredFetch)).Entities.Count > 0)
                        {
                            var SI_data = service.RetrieveMultiple(new FetchExpression(TotalSumInsuredFetch)).Entities;
                            var SI_field = "";
                            if (Buildings < 5000000)
                            {
                                SI_field = "lux_05m";
                            }
                            else if (Buildings >= 5000000 && Buildings < 25000000)
                            {
                                SI_field = "lux_5m25m";
                            }
                            else if (Buildings >= 25000000 && Buildings < 50000000)
                            {
                                SI_field = "lux_25m50m";
                            }
                            else if (Buildings >= 50000000 && Buildings < 100000000)
                            {
                                SI_field = "lux_50m100m";
                            }
                            else if (Buildings >= 100000000 && Buildings < 200000000)
                            {
                                SI_field = "lux_100m200m";
                            }
                            BuildingPerilsRate = SI_data.Sum(x => x.GetAttributeValue<decimal>(SI_field)) * 100;

                            if (Contents < 5000000)
                            {
                                SI_field = "lux_05m";
                            }
                            else if (Contents >= 5000000 && Contents < 25000000)
                            {
                                SI_field = "lux_5m25m";
                            }
                            else if (Contents >= 25000000 && Contents < 50000000)
                            {
                                SI_field = "lux_25m50m";
                            }
                            else if (Contents >= 50000000 && Contents < 100000000)
                            {
                                SI_field = "lux_50m100m";
                            }
                            else if (Contents >= 100000000 && Contents < 200000000)
                            {
                                SI_field = "lux_100m200m";
                            }

                            ContentsPerilsRate = SI_data.Sum(x => x.GetAttributeValue<decimal>(SI_field)) * 100;
                        }

                        if (BuildingPerilsRate < 0.049M)
                        {
                            BuildingPerilsRate = 0.049M;
                        }

                        if (ContentsPerilsRate < 0.049M)
                        {
                            ContentsPerilsRate = 0.049M;
                        }

                        var BuildingRate = BuildingFireRate + BuildingPerilsRate;
                        BuildingPremium = Buildings * BuildingRate / 100;

                        TotalBuildingPremium += BuildingPremium;
                        TotalBuildingFireRate += BuildingFireRate;
                        TotalBuildingPerilsRate += BuildingPerilsRate;

                        var ContentsRate = ContentsFireRate + ContentsPerilsRate;


                        if (Contents != 0)
                        {
                            ContentsPremium = Contents * ContentsRate / 100;
                            decimal TheftContentPremium = 0;

                            if (TotalContentsTheftRate != 0)
                            {
                                if (Contents <= 10000)
                                {
                                    TheftContentPremium = Contents * TotalContentsTheftRate / 100;
                                }
                                else
                                {
                                    if (Contents <= 30000)
                                    {
                                        TheftContentPremium = 10000 * TotalContentsTheftRate / 100;
                                        var remainingContent = Contents - 10000;
                                        TheftContentPremium += remainingContent * (TotalContentsTheftRate * 75 / 100) / 100;
                                    }
                                    else if (Contents <= 50000)
                                    {
                                        TheftContentPremium = 10000 * TotalContentsTheftRate / 100;
                                        TheftContentPremium += 20000 * (TotalContentsTheftRate * 75 / 100) / 100;

                                        var remainingContent1 = Contents - 30000;
                                        TheftContentPremium += remainingContent1 * (TotalContentsTheftRate * 50 / 100) / 100;
                                    }
                                    else if (Contents > 50000)
                                    {
                                        TheftContentPremium = 10000 * TotalContentsTheftRate / 100;
                                        TheftContentPremium += 20000 * (TotalContentsTheftRate * 75 / 100) / 100;
                                        TheftContentPremium += 20000 * (TotalContentsTheftRate * 50 / 100) / 100;

                                        var remainingContent1 = Contents - 50000;
                                        TheftContentPremium += remainingContent1 * (TotalContentsTheftRate * 25 / 100) / 100;
                                    }
                                }
                            }
                            ContentsPremium = ContentsPremium + TheftContentPremium;
                        }

                        TenentsPremium = Tenents * ContentsRate / 100;

                        TotalContentsPremium += ContentsPremium;
                        TotalContentsFireRate += ContentsFireRate;
                        TotalContentsPerilsRate += ContentsPerilsRate;
                        TotalTenentsPremium += TenentsPremium;

                        var Stock = premise_data.Attributes.Contains("lux_stockexcludinghighvaluestock") ? premise_data.GetAttributeValue<Money>("lux_stockexcludinghighvaluestock").Value : 0;

                        if (Stock <= 20000)
                        {
                            StockPremium = Stock * StockRate / 100;
                        }
                        else
                        {
                            if (Stock <= 60000)
                            {
                                StockPremium = 20000 * StockRate / 100;
                                var remainingContent = Stock - 20000;
                                StockPremium += remainingContent * (StockRate * 75 / 100) / 100;
                            }
                            else if (Stock <= 100000)
                            {
                                StockPremium = 20000 * StockRate / 100;
                                StockPremium += 40000 * (StockRate * 75 / 100) / 100;

                                var remainingContent1 = Stock - 60000;
                                StockPremium += remainingContent1 * (StockRate * 50 / 100) / 100;
                            }
                            else if (Stock > 100000)
                            {
                                StockPremium = 20000 * StockRate / 100;
                                StockPremium += 40000 * (StockRate * 75 / 100) / 100;
                                StockPremium += 40000 * (StockRate * 50 / 100) / 100;

                                var remainingContent1 = Stock - 100000;
                                StockPremium += remainingContent1 * (StockRate * 25 / 100) / 100;
                            }
                        }
                        TotalStockPremium += StockPremium;
                        TotalStockRate += StockRate;

                        var TargetStock = premise_data.GetAttributeValue<OptionSetValueCollection>("lux_listhighvaluestock");
                        var TargetStockSI = 0M;
                        if (TargetStock != null)
                        {
                            decimal WineSumInsured = premise_data.Attributes.Contains("lux_winesfortifiedwinesspiritsfinesuminsured") ? premise_data.GetAttributeValue<Money>("lux_winesfortifiedwinesspiritsfinesuminsured").Value : 0;
                            decimal NonFerrusInsured = premise_data.Attributes.Contains("lux_nonferrousmetalssuminsured") ? premise_data.GetAttributeValue<Money>("lux_nonferrousmetalssuminsured").Value : 0;
                            decimal MobileInsured = premise_data.Attributes.Contains("lux_mobilephonessuminsured") ? premise_data.GetAttributeValue<Money>("lux_mobilephonessuminsured").Value : 0;
                            decimal ComputerSumInsured = premise_data.Attributes.Contains("lux_computerequipmentsuminsured") ? premise_data.GetAttributeValue<Money>("lux_computerequipmentsuminsured").Value : 0;
                            decimal AlcoholSumInsured = premise_data.Attributes.Contains("lux_alcoholsuminsured") ? premise_data.GetAttributeValue<Money>("lux_alcoholsuminsured").Value : 0;
                            decimal AudioSumInsured = premise_data.Attributes.Contains("lux_audiovideoequipmentsuminsured") ? premise_data.GetAttributeValue<Money>("lux_audiovideoequipmentsuminsured").Value : 0;
                            decimal CigarettesSumInsured = premise_data.Attributes.Contains("lux_cigarettescigarsortobaccoproductssuminsur") ? premise_data.GetAttributeValue<Money>("lux_cigarettescigarsortobaccoproductssuminsur").Value : 0;
                            decimal ComputerGamesInsured = premise_data.Attributes.Contains("lux_computergamesandorconsolessuminsured") ? premise_data.GetAttributeValue<Money>("lux_computergamesandorconsolessuminsured").Value : 0;
                            decimal JewelleryInsured = premise_data.Attributes.Contains("lux_jewellerywatchessuminsured") ? premise_data.GetAttributeValue<Money>("lux_jewellerywatchessuminsured").Value : 0;
                            decimal PowerToolsSumInsured = premise_data.Attributes.Contains("lux_powertoolssuminsured") ? premise_data.GetAttributeValue<Money>("lux_powertoolssuminsured").Value : 0;
                            decimal FineArtSumInsured = premise_data.Attributes.Contains("lux_fineartsuminsured") ? premise_data.GetAttributeValue<Money>("lux_fineartsuminsured").Value : 0;
                            TargetStockPremium = WineSumInsured + NonFerrusInsured + MobileInsured + ComputerSumInsured + AlcoholSumInsured + AudioSumInsured + CigarettesSumInsured + ComputerGamesInsured + JewelleryInsured + PowerToolsSumInsured + FineArtSumInsured;
                            TargetStockSI = TargetStockPremium;

                            if (TargetStockSI <= 20000)
                            {
                                TargetStockPremium = TargetStockSI * StockRate / 100;
                            }
                            else
                            {
                                if (TargetStockSI <= 60000)
                                {
                                    TargetStockPremium = 20000 * StockRate / 100;
                                    var remainingContent = TargetStockSI - 20000;
                                    TargetStockPremium += remainingContent * (StockRate * 75 / 100) / 100;
                                }
                                else if (TargetStockSI <= 100000)
                                {
                                    TargetStockPremium = 20000 * StockRate / 100;
                                    TargetStockPremium += 40000 * (StockRate * 75 / 100) / 100;

                                    var remainingContent1 = TargetStockSI - 60000;
                                    TargetStockPremium += remainingContent1 * (StockRate * 50 / 100) / 100;
                                }
                                else if (TargetStockSI > 100000)
                                {
                                    TargetStockPremium = 20000 * StockRate / 100;
                                    TargetStockPremium += 40000 * (StockRate * 75 / 100) / 100;
                                    TargetStockPremium += 40000 * (StockRate * 50 / 100) / 100;

                                    var remainingContent1 = TargetStockSI - 100000;
                                    TargetStockPremium += remainingContent1 * (StockRate * 25 / 100) / 100;
                                }
                            }
                            TotalTargetStockPremium += TargetStockPremium;
                            TotalTargetStockRate += Convert.ToDecimal(2);
                        }

                        if (ComputerEquipment != 0)
                        {
                            ComputerEquipmentPremium = ComputerEquipment * Convert.ToDecimal(0.5) / 100;

                            TotalComputerEquipmentPremium += ComputerEquipmentPremium;
                            TotalComputerEquipmentRate += Convert.ToDecimal(0.5);
                        }

                        var LossofRent = premise_data.Attributes.Contains("lux_materialdamagelossofrentpayable") ? premise_data.GetAttributeValue<Money>("lux_materialdamagelossofrentpayable").Value : 0;
                        if (LossofRent != 0)
                        {
                            LossofRentPremium = LossofRent * Convert.ToDecimal(LORRate) / 100;

                            TotalLossofRentPremium += LossofRentPremium;
                            TotalLORRate += LORRate;
                        }

                        var totalPremium = BuildingPremium + ContentsPremium + TenentsPremium + StockPremium + TargetStockPremium + ComputerEquipmentPremium + LossofRentPremium;
                        totalPremium = totalPremium * dateDiffDays / 365;

                        var TotalSI = Buildings + Contents + Tenents + ComputerEquipment + LossofRent + TargetStockSI + Stock;

                        var item1 = service.Retrieve(entityName, item.Id, new ColumnSet(true));

                        item1["lux_buildingsdeclaredvalue"] = new Money(Buildings);
                        item1["lux_tenantsimprovementsdeclaredvalue"] = new Money(Tenents);
                        item1["lux_generalcontentsdeclaredvalueincludingmach"] = new Money(Contents);
                        item1["lux_computerandelectronicbusinessequipment"] = new Money(ComputerEquipment);
                        item1["lux_totalmdsuminsured"] = new Money(TotalSI);

                        if (item.GetAttributeValue<bool>("lux_isdayoneupliftcoverrequired") == true)
                        {
                            var indexingValue = item.Attributes.Contains("lux_dayoneupliftcover") ? item.FormattedValues["lux_dayoneupliftcover"].ToString() : "";
                            if (indexingValue != "")
                            {
                                var indexed = Convert.ToDecimal(indexingValue.Replace("%", ""));
                                var Amount = Buildings + Tenents + Contents;
                                var upliftedAmount = Amount + Amount * indexed / 100 + Stock + TargetStockSI + ComputerEquipment + LossofRent;
                                item1["lux_totalmdsuminsuredwithupliftedamount"] = new Money(upliftedAmount);
                                item1["lux_buildingssuminsuredwithupliftedamount"] = new Money(Buildings + Buildings * indexed / 100);
                                item1["lux_tenantssuminsuredwithupliftedamount"] = new Money(Tenents + Tenents * indexed / 100);
                                item1["lux_contentssuminsuredwithupliftedamount"] = new Money(Contents + Contents * indexed / 100);
                            }
                        }
                        else
                        {
                            item1["lux_totalmdsuminsuredwithupliftedamount"] = new Money(TotalSI);
                            item1["lux_buildingssuminsuredwithupliftedamount"] = new Money(Buildings);
                            item1["lux_tenantssuminsuredwithupliftedamount"] = new Money(Tenents);
                            item1["lux_contentssuminsuredwithupliftedamount"] = new Money(Contents);
                        }


                        if (totalPremium < Convert.ToDecimal(50))
                        {
                            totalPremium = Convert.ToDecimal(50);
                            item1["lux_materialdamagepremium"] = new Money(totalPremium);
                        }
                        else
                        {
                            item1["lux_materialdamagepremium"] = new Money(totalPremium);
                        }

                        item1["lux_buildingsfirerate"] = BuildingFireRate;
                        item1["lux_buildingsperilsrate"] = BuildingPerilsRate;
                        item1["lux_contentsfirerate"] = ContentsFireRate;
                        item1["lux_contentsperilsrate"] = ContentsPerilsRate;
                        item1["lux_stockrate"] = StockRate;
                        item1["lux_targetstockrate"] = Convert.ToDecimal(3);
                        item1["lux_computerequipmentrate"] = Convert.ToDecimal(0.5);
                        item1["lux_lossofrentpayablerate"] = LORRate;
                        service.Update(item1);

                        TotalMDPremium += totalPremium;
                    }
                    else
                    {
                        TotalMDPremium += 50;

                        var item1 = service.Retrieve(entityName, item.Id, new ColumnSet(true));

                        item1["lux_buildingsdeclaredvalue"] = new Money(Buildings);
                        item1["lux_tenantsimprovementsdeclaredvalue"] = new Money(Tenents);
                        item1["lux_generalcontentsdeclaredvalueincludingmach"] = new Money(Contents);
                        item1["lux_computerandelectronicbusinessequipment"] = new Money(ComputerEquipment);

                        item1["lux_materialdamagepremium"] = new Money(50);
                        item1["lux_buildingsfirerate"] = Convert.ToDecimal(0);
                        item1["lux_buildingsperilsrate"] = Convert.ToDecimal(0);
                        item1["lux_contentsfirerate"] = Convert.ToDecimal(0);
                        item1["lux_contentsperilsrate"] = Convert.ToDecimal(0);
                        item1["lux_stockrate"] = Convert.ToDecimal(0);
                        item1["lux_targetstockrate"] = Convert.ToDecimal(0);
                        item1["lux_computerequipmentrate"] = Convert.ToDecimal(0);
                        item1["lux_lossofrentpayablerate"] = Convert.ToDecimal(0);
                        service.Update(item1);
                    }
                }

                if (TotalMDPremium < 100)
                {
                    TotalMDPremium = 100;
                }

                appln["lux_retailmdpremium"] = new Money(TotalMDPremium);
                appln["lux_materialdamagepolicypremium"] = new Money(TotalMDPremium);

                appln["lux_buildingpremium"] = new Money(TotalBuildingPremium * dateDiffDays / 365);
                appln["lux_contentspremium"] = new Money(TotalContentsPremium * dateDiffDays / 365);
                appln["lux_tenentspremium"] = new Money(TotalTenentsPremium * dateDiffDays / 365);
                appln["lux_stockpremium"] = Convert.ToDecimal(StockPremium * dateDiffDays / 365);
                appln["lux_targetstockpremium"] = Convert.ToDecimal(TargetStockPremium * dateDiffDays / 365);
                appln["lux_computerequipmentpremium"] = Convert.ToDecimal(ComputerEquipmentPremium * dateDiffDays / 365);
                appln["lux_lossofrentpremium"] = Convert.ToDecimal(LossofRentPremium * dateDiffDays / 365);

                appln["lux_buildingfirerate"] = Convert.ToDecimal(TotalBuildingFireRate / PremiseCount);
                appln["lux_buildingperilsrate"] = Convert.ToDecimal(TotalBuildingPerilsRate / PremiseCount);
                appln["lux_contentsfirerate"] = Convert.ToDecimal(TotalContentsFireRate / PremiseCount);
                appln["lux_contentsperilsrate"] = Convert.ToDecimal(TotalContentsPerilsRate / PremiseCount);
                appln["lux_stockrate"] = Convert.ToDecimal(TotalStockRate / PremiseCount);
                appln["lux_targetstockrate"] = Convert.ToDecimal(TotalTargetStockRate / PremiseCount);
                appln["lux_computerequipmentrate"] = Convert.ToDecimal(TotalComputerEquipmentRate / PremiseCount);
                appln["lux_lossofrentrate"] = Convert.ToDecimal(TotalLORRate / PremiseCount);


                //BI Premium
                var CoverBasis = appln.GetAttributeValue<OptionSetValue>("lux_typeofcover");
                if (CoverBasis != null)
                {
                    decimal GrossProfitRevenue = appln.Attributes.Contains("lux_amount") ? appln.GetAttributeValue<Money>("lux_amount").Value : 0;
                    decimal IncreasedCOW = appln.Attributes.Contains("lux_icow") ? appln.GetAttributeValue<Money>("lux_icow").Value : 0;
                    decimal AdditionalIncreasedCOW = appln.Attributes.Contains("lux_additionalincreasedcostofworking") ? appln.GetAttributeValue<Money>("lux_additionalincreasedcostofworking").Value : 0;
                    decimal BookDebts = appln.Attributes.Contains("lux_bookdebts") ? appln.GetAttributeValue<Money>("lux_bookdebts").Value : 0;
                    decimal LOR = appln.Attributes.Contains("lux_rentreceivable") ? appln.GetAttributeValue<Money>("lux_rentreceivable").Value : 0;

                    var LOLRequired = appln.Attributes.Contains("lux_lossoflicense") ? appln.GetAttributeValue<bool>("lux_lossoflicense") : false;

                    if (LOLRequired == true)
                    {
                        var lolAmount = appln.Attributes.Contains("lux_lossoflicenseindemnitylimit") ? appln.GetAttributeValue<OptionSetValue>("lux_lossoflicenseindemnitylimit").Value : 0;
                        if (lolAmount == 972970002)
                        {
                            LORAmount = 250000;
                        }
                        else
                        {
                            LORAmount = 100000;
                        }
                    }

                    TotalBISumInsured = GrossProfitRevenue + IncreasedCOW + AdditionalIncreasedCOW + BookDebts + LOR + LORAmount;

                    decimal GPRRate = 0;
                    decimal LORRate = 0;

                    if (productName != "Contractors Combined")
                    {
                        if (service.RetrieveMultiple(new FetchExpression(FireRateFetch)).Entities.Count > 0)
                        {
                            var FireData = service.RetrieveMultiple(new FetchExpression(FireRateFetch)).Entities[0];
                            LORRate = FireData.GetAttributeValue<decimal>("lux_mdfirerate");
                            GPRRate = FireData.GetAttributeValue<decimal>("lux_blfirerate");

                            if (LORRate < 0.10M)
                            {
                                LORRate = 0.10M;
                            }
                            if (GPRRate < 0.06M)
                            {
                                GPRRate = 0.06M;
                            }

                            if (productName == "Pubs & Restaurants" || productName == "Hotels and Guesthouses")
                            {
                                if (appln.GetAttributeValue<OptionSetValue>("lux_pubsrestaurantproducttype").Value == 972970001 || appln.GetAttributeValue<OptionSetValue>("lux_pubsrestaurantproducttype").Value == 972970003)
                                {
                                    GPRRate = 0.096M;
                                }
                                else if (appln.GetAttributeValue<OptionSetValue>("lux_pubsrestaurantproducttype").Value == 972970002)
                                {
                                    LORRate = 0.14M;
                                    GPRRate = 0.084M;
                                }
                            }
                            if (productName == "Commercial Combined")
                            {
                                if (LORRate < 0.10M)
                                {
                                    LORRate = 0.10M;
                                }

                                if (FireData.FormattedValues["lux_tradesector"].ToString().Contains("Wholesaler's") || FireData.FormattedValues["lux_tradesector"].ToString().Contains("Business") || FireData.FormattedValues["lux_tradesector"].ToString().Contains("Leisure"))
                                {
                                    if (FireData.GetAttributeValue<int>("lux_mdbi") == 2)
                                    {
                                        LORRate = 0.10M;
                                        GPRRate = 0.06M;
                                    }
                                    else if (FireData.GetAttributeValue<int>("lux_mdbi") == 3)
                                    {
                                        LORRate = 0.125M;
                                        GPRRate = 0.075M;
                                    }
                                    else if (FireData.GetAttributeValue<int>("lux_mdbi") == 4)
                                    {
                                        LORRate = 0.175M;
                                        GPRRate = 0.105M;
                                    }
                                    else if (FireData.GetAttributeValue<int>("lux_mdbi") == 5)
                                    {
                                        LORRate = 0.225M;
                                        GPRRate = 0.135M;
                                    }
                                    else if (FireData.GetAttributeValue<int>("lux_mdbi") == 6)
                                    {
                                        LORRate = 0.00M;
                                        GPRRate = 0.00M;
                                    }
                                }

                                if (FireData.FormattedValues["lux_tradesector"].ToString().Contains("Manufacturer's"))
                                {
                                    if (FireData.GetAttributeValue<int>("lux_mdbi") == 2)
                                    {
                                        LORRate = 0.10M;
                                        GPRRate = 0.06M;
                                    }
                                    else if (FireData.GetAttributeValue<int>("lux_mdbi") == 3)
                                    {
                                        LORRate = 0.15M;
                                        GPRRate = 0.09M;
                                    }
                                    else if (FireData.GetAttributeValue<int>("lux_mdbi") == 4)
                                    {
                                        LORRate = 0.20M;
                                        GPRRate = 0.12M;
                                    }
                                    else if (FireData.GetAttributeValue<int>("lux_mdbi") == 5)
                                    {
                                        LORRate = 0.25M;
                                        GPRRate = 0.15M;
                                    }
                                    else if (FireData.GetAttributeValue<int>("lux_mdbi") == 6)
                                    {
                                        LORRate = 0.00M;
                                        GPRRate = 0.00M;
                                    }
                                }
                            }
                        }

                        var GrossPRPremium = GrossProfitRevenue * Convert.ToDecimal(GPRRate) / 100;
                        var IncreasedCOWPremium = IncreasedCOW * Convert.ToDecimal(GPRRate * 2) / 100;
                        var AdditionalIncreasedCOWPremium = AdditionalIncreasedCOW * Convert.ToDecimal(GPRRate * 4) / 100;
                        var LORPremium = LOR * Convert.ToDecimal(LORRate) / 100;

                        TotalGrossProfitRevenuePremium += GrossPRPremium;
                        TotalAdditionalIncreasedICOWPremium += AdditionalIncreasedCOWPremium;
                        TotalIncreasedICOWPremium += IncreasedCOWPremium;
                        TotalLORPremium += LORPremium;

                        TotalGrossProfitRevenueRate += GPRRate;
                        TotalIncreasedICOWRate += GPRRate * 2;
                        TotalAdditionalIncreasedICOWRate += GPRRate * 4;
                        TotalLORRate += LORRate;

                        var totalPremium = GrossPRPremium + IncreasedCOWPremium + AdditionalIncreasedCOWPremium + LORPremium;

                        TotalBIPremium += totalPremium;
                    }
                    else if (productName == "Contractors Combined")
                    {
                        if (service.RetrieveMultiple(new FetchExpression(FireRateFetch)).Entities.Count > 0)
                        {
                            var FireData = service.RetrieveMultiple(new FetchExpression(FireRateFetch)).Entities[0];
                            GPRRate = FireData.GetAttributeValue<decimal>("lux_businessinterruptionrate");
                            LORRate = FireData.GetAttributeValue<decimal>("lux_businessinterruptionrate");
                        }

                        var GrossPRPremium = GrossProfitRevenue * Convert.ToDecimal(GPRRate) / 100;
                        var IncreasedCOWPremium = IncreasedCOW * Convert.ToDecimal(GPRRate * 2) / 100;
                        var AdditionalIncreasedCOWPremium = AdditionalIncreasedCOW * Convert.ToDecimal(GPRRate * 4) / 100;
                        var LORPremium = LOR * Convert.ToDecimal(LORRate) / 100;

                        TotalGrossProfitRevenuePremium += GrossPRPremium;
                        TotalAdditionalIncreasedICOWPremium += AdditionalIncreasedCOWPremium;
                        TotalIncreasedICOWPremium += IncreasedCOWPremium;
                        TotalLORPremium += LORPremium;

                        TotalGrossProfitRevenueRate += GPRRate;
                        TotalIncreasedICOWRate += GPRRate * 2;
                        TotalAdditionalIncreasedICOWRate += GPRRate * 4;
                        TotalLORRate += LORRate;

                        var totalPremium = GrossPRPremium + IncreasedCOWPremium + AdditionalIncreasedCOWPremium + LORPremium;

                        TotalBIPremium += totalPremium;
                    }
                }

                TotalBIPremium = TotalBIPremium * dateDiffDays / 365;

                if (TotalBIPremium < 75)
                {
                    TotalBIPremium = 75;
                }
                appln["lux_retailbipremium"] = new Money(TotalBIPremium);
                appln["lux_totalbisuminsured"] = new Money(TotalBISumInsured);
                appln["lux_grossprofitorrevenuerate"] = Convert.ToDecimal(TotalGrossProfitRevenueRate);
                appln["lux_grossprofitorrevenuepremium"] = new Money(TotalGrossProfitRevenuePremium * dateDiffDays / 365);
                appln["lux_increasedcostofworkingrate"] = Convert.ToDecimal(TotalAdditionalIncreasedICOWRate);
                appln["lux_increasedcostofworkingpremium"] = new Money(TotalIncreasedICOWPremium * dateDiffDays / 365);
                appln["lux_additionalincreasedcostofworkingrate"] = Convert.ToDecimal(TotalAdditionalIncreasedICOWRate);
                appln["lux_additionalincreasedcostofworkingpremium"] = new Money(TotalAdditionalIncreasedICOWPremium * dateDiffDays / 365);
                appln["lux_bilossofrentrate"] = Convert.ToDecimal(TotalLORRate);
                appln["lux_bilossofrentpremium"] = new Money(TotalLORPremium * dateDiffDays / 365);
                if (productName == "Commercial Combined")
                {
                    if (appln.GetAttributeValue<bool>("lux_isbusinessinterruptioncoverrequired") == false)
                    {
                        TotalBIPremium = 0;
                        appln["lux_retailbipremium"] = new Money(TotalBIPremium);
                        appln["lux_totalbisuminsured"] = new Money(0);
                        appln["lux_grossprofitorrevenuerate"] = Convert.ToDecimal(0);
                        appln["lux_grossprofitorrevenuepremium"] = new Money(0);
                        appln["lux_increasedcostofworkingrate"] = Convert.ToDecimal(0);
                        appln["lux_increasedcostofworkingpremium"] = new Money(0);
                        appln["lux_additionalincreasedcostofworkingrate"] = Convert.ToDecimal(0);
                        appln["lux_additionalincreasedcostofworkingpremium"] = new Money(0);
                        appln["lux_bilossofrentrate"] = Convert.ToDecimal(0);
                        appln["lux_bilossofrentpremium"] = new Money(0);
                    }
                }

                //Terrorism Premium
                if (appln.Attributes.Contains("lux_isterrorismcoverrequired") && appln.GetAttributeValue<bool>("lux_isterrorismcoverrequired") == true)
                {
                    decimal TerrorismTotal = 0;
                    foreach (var item in service.RetrieveMultiple(new FetchExpression(fetch)).Entities)
                    {
                        var premise_data = item;

                        var postcode = premise_data.Contains("lux_riskpostcode") ? premise_data.Attributes["lux_riskpostcode"] : "";
                        var post2digits = postcode.ToString().Substring(0, 2);
                        var post3digits = postcode.ToString().Substring(0, 3);
                        var post4digits = postcode.ToString().Substring(0, 4);
                        var zone = 972970003;
                        if (postcode.ToString() != "")
                        {
                            var TerrorismFetch = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                                                          <entity name='lux_terrorismratingzone'>
                                                            <attribute name='lux_name' />
                                                            <attribute name='lux_locationzone' />
                                                            <attribute name='lux_terrorismratingzoneid' />
                                                            <order attribute='lux_locationzone' descending='false' />
                                                            <order attribute='lux_name' descending='false' />
                                                            <filter type='and'>
                                                              <condition attribute='statecode' operator='eq' value='0' />
                                                              <condition attribute='lux_name' operator='eq' value='{post4digits}' />
                                                            </filter>
                                                          </entity>
                                                        </fetch>";
                            if (service.RetrieveMultiple(new FetchExpression(TerrorismFetch)).Entities.Count > 0)
                            {
                                zone = service.RetrieveMultiple(new FetchExpression(TerrorismFetch)).Entities[0].GetAttributeValue<OptionSetValue>("lux_locationzone").Value;
                            }
                            else
                            {
                                var TerrorismFetch1 = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                                                          <entity name='lux_terrorismratingzone'>
                                                            <attribute name='lux_name' />
                                                            <attribute name='lux_locationzone' />
                                                            <attribute name='lux_terrorismratingzoneid' />
                                                            <order attribute='lux_locationzone' descending='false' />
                                                            <order attribute='lux_name' descending='false' />
                                                            <filter type='and'>
                                                              <condition attribute='statecode' operator='eq' value='0' />
                                                              <condition attribute='lux_name' operator='eq' value='{post3digits}' />
                                                            </filter>
                                                          </entity>
                                                        </fetch>";
                                if (service.RetrieveMultiple(new FetchExpression(TerrorismFetch1)).Entities.Count > 0)
                                {
                                    zone = service.RetrieveMultiple(new FetchExpression(TerrorismFetch1)).Entities[0].GetAttributeValue<OptionSetValue>("lux_locationzone").Value;
                                }
                                else
                                {
                                    var TerrorismFetch2 = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                                                          <entity name='lux_terrorismratingzone'>
                                                            <attribute name='lux_name' />
                                                            <attribute name='lux_locationzone' />
                                                            <attribute name='lux_terrorismratingzoneid' />
                                                            <order attribute='lux_locationzone' descending='false' />
                                                            <order attribute='lux_name' descending='false' />
                                                            <filter type='and'>
                                                              <condition attribute='statecode' operator='eq' value='0' />
                                                              <condition attribute='lux_name' operator='eq' value='{post2digits}' />
                                                            </filter>
                                                          </entity>
                                                        </fetch>";
                                    if (service.RetrieveMultiple(new FetchExpression(TerrorismFetch2)).Entities.Count > 0)
                                    {
                                        zone = service.RetrieveMultiple(new FetchExpression(TerrorismFetch2)).Entities[0].GetAttributeValue<OptionSetValue>("lux_locationzone").Value;
                                    }
                                }
                            }
                        }

                        var IsBITaken = appln.GetAttributeValue<OptionSetValue>("lux_typeofcover");
                        decimal BIsum_Insured = 0;
                        if (IsBITaken != null)
                        {
                            decimal GrossProfitRevenue = appln.Attributes.Contains("lux_amount") ? appln.GetAttributeValue<Money>("lux_amount").Value : 0;
                            decimal IncreasedCOW = appln.Attributes.Contains("lux_icow") ? appln.GetAttributeValue<Money>("lux_icow").Value : 0;
                            decimal AdditionalIncreasedCOW = appln.Attributes.Contains("lux_additionalincreasedcostofworking") ? appln.GetAttributeValue<Money>("lux_additionalincreasedcostofworking").Value : 0;
                            decimal BookDebts = appln.Attributes.Contains("lux_bookdebts") ? appln.GetAttributeValue<Money>("lux_bookdebts").Value : 0;
                            decimal LOR = appln.Attributes.Contains("lux_rentreceivable") ? appln.GetAttributeValue<Money>("lux_rentreceivable").Value : 0;
                            BIsum_Insured = GrossProfitRevenue + IncreasedCOW + AdditionalIncreasedCOW + LOR;
                        }

                        var Buildings = premise_data.Attributes.Contains("lux_buildingsdeclaredvalue") ? premise_data.GetAttributeValue<Money>("lux_buildingsdeclaredvalue").Value : 0;
                        var Tenents = premise_data.Attributes.Contains("lux_tenantsimprovementsdeclaredvalue") ? premise_data.GetAttributeValue<Money>("lux_tenantsimprovementsdeclaredvalue").Value : 0;
                        var Contents = premise_data.Attributes.Contains("lux_generalcontentsdeclaredvalueincludingmach") ? premise_data.GetAttributeValue<Money>("lux_generalcontentsdeclaredvalueincludingmach").Value : 0;
                        var ComputerEquip = premise_data.Attributes.Contains("lux_computerandelectronicbusinessequipment") ? premise_data.GetAttributeValue<Money>("lux_computerandelectronicbusinessequipment").Value : 0;
                        var Stock = premise_data.Attributes.Contains("lux_stockexcludinghighvaluestock") ? premise_data.GetAttributeValue<Money>("lux_stockexcludinghighvaluestock").Value : 0;
                        decimal TargetStockSI = 0;
                        var TargetStock = premise_data.GetAttributeValue<OptionSetValueCollection>("lux_listhighvaluestock");
                        if (TargetStock != null)
                        {
                            decimal WineSumInsured = premise_data.Attributes.Contains("lux_winesfortifiedwinesspiritsfinesuminsured") ? premise_data.GetAttributeValue<Money>("lux_winesfortifiedwinesspiritsfinesuminsured").Value : 0;
                            decimal NonFerrusInsured = premise_data.Attributes.Contains("lux_nonferrousmetalssuminsured") ? premise_data.GetAttributeValue<Money>("lux_nonferrousmetalssuminsured").Value : 0;
                            decimal MobileInsured = premise_data.Attributes.Contains("lux_mobilephonessuminsured") ? premise_data.GetAttributeValue<Money>("lux_mobilephonessuminsured").Value : 0;
                            decimal ComputerSumInsured = premise_data.Attributes.Contains("lux_computerequipmentsuminsured") ? premise_data.GetAttributeValue<Money>("lux_computerequipmentsuminsured").Value : 0;
                            decimal AlcoholSumInsured = premise_data.Attributes.Contains("lux_alcoholsuminsured") ? premise_data.GetAttributeValue<Money>("lux_alcoholsuminsured").Value : 0;
                            decimal AudioSumInsured = premise_data.Attributes.Contains("lux_audiovideoequipmentsuminsured") ? premise_data.GetAttributeValue<Money>("lux_audiovideoequipmentsuminsured").Value : 0;
                            decimal CigarettesSumInsured = premise_data.Attributes.Contains("lux_cigarettescigarsortobaccoproductssuminsur") ? premise_data.GetAttributeValue<Money>("lux_cigarettescigarsortobaccoproductssuminsur").Value : 0;
                            decimal ComputerGamesInsured = premise_data.Attributes.Contains("lux_computergamesandorconsolessuminsured") ? premise_data.GetAttributeValue<Money>("lux_computergamesandorconsolessuminsured").Value : 0;
                            decimal JewelleryInsured = premise_data.Attributes.Contains("lux_jewellerywatchessuminsured") ? premise_data.GetAttributeValue<Money>("lux_jewellerywatchessuminsured").Value : 0;
                            decimal PowerToolsSumInsured = premise_data.Attributes.Contains("lux_powertoolssuminsured") ? premise_data.GetAttributeValue<Money>("lux_powertoolssuminsured").Value : 0;
                            decimal FineArtSumInsured = premise_data.Attributes.Contains("lux_fineartsuminsured") ? premise_data.GetAttributeValue<Money>("lux_fineartsuminsured").Value : 0;
                            TargetStockSI = WineSumInsured + NonFerrusInsured + MobileInsured + ComputerSumInsured + AlcoholSumInsured + AudioSumInsured + CigarettesSumInsured + ComputerGamesInsured + JewelleryInsured + PowerToolsSumInsured + FineArtSumInsured;
                        }
                        var LossofRent = premise_data.Attributes.Contains("lux_materialdamagelossofrentpayable") ? premise_data.GetAttributeValue<Money>("lux_materialdamagelossofrentpayable").Value : 0;
                        decimal SARSI = 0;
                        var fetch11 = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                              <entity name='lux_allriskitem'>
                                <attribute name='lux_typeofequipment' />
                                <attribute name='lux_territoriallimit' />
                                <attribute name='lux_suminsured' />
                                <attribute name='lux_excess' />
                                <attribute name='lux_allriskitemid' />
                                <order attribute='lux_typeofequipment' descending='false' />
                                <filter type='and'>
                                  <condition attribute='statecode' operator='eq' value='0' />
                                  <condition attribute='lux_application' operator='eq' uiname='' uitype='lux_propertyownersapplications' value='{appln.Id}' />
                                </filter>
                                <link-entity name='lux_propertyownersapplications' from='lux_propertyownersapplicationsid' to='lux_application' link-type='inner' alias='aa'>
                                  <filter type='and'>
                                    <condition attribute='lux_allriskitems' operator='eq' value='1' />
                                  </filter>
                                </link-entity>
                              </entity>
                            </fetch>";

                        if (service.RetrieveMultiple(new FetchExpression(fetch11)).Entities.Count > 0)
                        {
                            foreach (var item11 in service.RetrieveMultiple(new FetchExpression(fetch11)).Entities)
                            {
                                SARSI += item11.Attributes.Contains("lux_suminsured") ? item11.GetAttributeValue<Money>("lux_suminsured").Value : 0;
                            }
                        }

                        var MDSum_insured = Buildings + Tenents + Contents + ComputerEquip + Stock + TargetStockSI + LossofRent + SARSI;

                        decimal TerrorismPremium = 0;
                        decimal TerrorismMDPremium = 0;
                        decimal TerrorismBIPremium = 0;
                        decimal MDSI_rate = 0;
                        decimal BISI_rate = 0;

                        if (MDSum_insured > 0)
                        {
                            var MDRatesFetch = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                                              <entity name='lux_terrorismrate'>
                                                <attribute name='lux_ratebeforeanydiscount' />
                                                <attribute name='lux_locationzone' />
                                                <attribute name='lux_ratetype' />
                                                <attribute name='lux_terrorismrateid' />
                                                <order attribute='lux_ratetype' descending='false' />
                                                <order attribute='lux_locationzone' descending='false' />
                                                <filter type='and'>
                                                  <condition attribute='statecode' operator='eq' value='0' />
                                                  <condition attribute='lux_locationzone' operator='eq' value='{zone}' />
                                                  <condition attribute='lux_ratetype' operator='eq' value='972970002' />
                                                </filter>
                                              </entity>
                                            </fetch>";

                            if (service.RetrieveMultiple(new FetchExpression(MDRatesFetch)).Entities.Count > 0)
                            {
                                var SI_data = service.RetrieveMultiple(new FetchExpression(MDRatesFetch)).Entities[0];
                                if (SI_data.Contains("lux_ratebeforeanydiscount"))
                                {
                                    MDSI_rate = SI_data.GetAttributeValue<decimal>("lux_ratebeforeanydiscount");
                                }
                                TerrorismMDPremium = MDSum_insured * MDSI_rate / 100;
                            }
                        }
                        if (BIsum_Insured > 0)
                        {
                            var BIRatesFetch = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                                              <entity name='lux_terrorismrate'>
                                                <attribute name='lux_ratebeforeanydiscount' />
                                                <attribute name='lux_locationzone' />
                                                <attribute name='lux_ratetype' />
                                                <attribute name='lux_terrorismrateid' />
                                                <order attribute='lux_ratetype' descending='false' />
                                                <order attribute='lux_locationzone' descending='false' />
                                                <filter type='and'>
                                                  <condition attribute='statecode' operator='eq' value='0' />
                                                  <condition attribute='lux_locationzone' operator='eq' value='{zone}' />
                                                  <condition attribute='lux_ratetype' operator='eq' value='972970001' />
                                                </filter>
                                              </entity>
                                            </fetch>";

                            if (service.RetrieveMultiple(new FetchExpression(BIRatesFetch)).Entities.Count > 0)
                            {
                                var SI_data = service.RetrieveMultiple(new FetchExpression(BIRatesFetch)).Entities[0];
                                if (SI_data.Contains("lux_ratebeforeanydiscount"))
                                {
                                    BISI_rate = SI_data.GetAttributeValue<decimal>("lux_ratebeforeanydiscount");
                                }
                                TerrorismBIPremium += BIsum_Insured * BISI_rate / 100;
                            }
                        }
                        TerrorismPremium = TerrorismMDPremium + TerrorismBIPremium;

                        //var MDSIDisk = Buildings + Contents + Stock + TargetStockSI;
                        //if (MDSIDisk < 2000000)
                        //{
                        //    TerrorismPremium = TerrorismPremium - TerrorismPremium * 40 / 100;
                        //}

                        var item1 = service.Retrieve(entityName, item.Id, new ColumnSet(true));
                        item1["lux_terrorismbipremium"] = new Money(TerrorismBIPremium);
                        item1["lux_terrorismmdpremium"] = new Money(TerrorismMDPremium);
                        item1["lux_terrorismbirate"] = BISI_rate;
                        item1["lux_terrorismmdrate"] = MDSI_rate;
                        item1["lux_terrorismpremium"] = new Money(TerrorismPremium);
                        item1["lux_terrorismzone"] = new OptionSetValue(zone);
                        service.Update(item1);

                        TerrorismTotal += TerrorismPremium;
                    }

                    if (inceptionDate >= new DateTime(2025, 05, 01))
                    {
                        appln["lux_terrorismpremium"] = new Money(TerrorismTotal);
                        appln["lux_terrorismnetpremium"] = new Money(TerrorismTotal - TerrorismTotal * Convert.ToDecimal(37.5) / 100);

                        appln["lux_terrorismquotedpremium"] = new Money(TerrorismTotal);
                        appln["lux_terrorismpolicynetpremiumexcludingipt"] = new Money(TerrorismTotal - TerrorismTotal * Convert.ToDecimal(37.5) / 100);

                        appln["lux_terrorismbrokercommission"] = "22.5%";
                        appln["lux_terrorismbrokercommissionamount"] = new Money(TerrorismTotal * 22.5M / 100);
                        appln["lux_terrorismpolicybrokercommission"] = "22.5%";
                        appln["lux_terrorismquotedpremiumbrokercommissionamo"] = new Money(TerrorismTotal * 22.5M / 100);
                        appln["lux_terrorismaciescommission"] = "15.0%";
                        appln["lux_terrorismaciescommissionamout"] = new Money(TerrorismTotal * 15M / 100);
                        appln["lux_terrorismpolicyaciescommission"] = "15.0%";
                        appln["lux_terrorismquotedpremiumaciescommissionamou"] = new Money(TerrorismTotal * 15M / 100);
                        appln["lux_terrorismtotalcommission"] = "37.5%";
                        appln["lux_terrorismpolicytotalcommission"] = "37.5%";
                    }
                    else
                    {
                        appln["lux_terrorismpremium"] = new Money(TerrorismTotal);
                        appln["lux_terrorismnetpremium"] = new Money(TerrorismTotal - TerrorismTotal * Convert.ToDecimal(32.5) / 100);

                        appln["lux_terrorismquotedpremium"] = new Money(TerrorismTotal);
                        appln["lux_terrorismpolicynetpremiumexcludingipt"] = new Money(TerrorismTotal - TerrorismTotal * Convert.ToDecimal(32.5) / 100);

                        appln["lux_terrorismbrokercommission"] = "20%";
                        appln["lux_terrorismbrokercommissionamount"] = new Money(TerrorismTotal * 20 / 100);
                        appln["lux_terrorismpolicybrokercommission"] = "20%";
                        appln["lux_terrorismquotedpremiumbrokercommissionamo"] = new Money(TerrorismTotal * 20 / 100);
                        appln["lux_terrorismaciescommission"] = "12.5%";
                        appln["lux_terrorismaciescommissionamout"] = new Money(TerrorismTotal * 12.5M / 100);
                        appln["lux_terrorismpolicyaciescommission"] = "12.5%";
                        appln["lux_terrorismquotedpremiumaciescommissionamou"] = new Money(TerrorismTotal * 12.5M / 100);
                        appln["lux_terrorismtotalcommission"] = "32.5%";
                        appln["lux_terrorismpolicytotalcommission"] = "32.5%";
                    }
                }

                //EL Premium
                if (productName != "Contractors Combined")
                {
                    if (appln.GetAttributeValue<bool>("lux_iselcoverrequired") == true)
                    {
                        var clerical = appln.Contains("lux_clericalcommercialandmanagerialwageroll") ? appln.GetAttributeValue<Money>("lux_clericalcommercialandmanagerialwageroll").Value : 0;
                        var manual = appln.Contains("lux_manualwageroll") ? appln.GetAttributeValue<Money>("lux_manualwageroll").Value : 0;
                        decimal workaway = 0;
                        decimal ELRate = 0;
                        if (appln.Attributes.Contains("lux_isanyworkawaycarriedoutotherthanforcollec") && appln.GetAttributeValue<bool>("lux_isanyworkawaycarriedoutotherthanforcollec") == true)
                        {
                            workaway = appln.Contains("lux_workawaywageroll") ? appln.GetAttributeValue<Money>("lux_workawaywageroll").Value : 0;
                        }

                        if (service.RetrieveMultiple(new FetchExpression(FireRateFetch)).Entities.Count > 0)
                        {
                            var ELData = service.RetrieveMultiple(new FetchExpression(FireRateFetch)).Entities[0];
                            if (productName == "Pubs & Restaurants" || productName == "Hotels and Guesthouses")
                            {
                                ELRate = 0.4M;
                            }
                            else if (productName == "Retail")
                            {
                                ELRate = 0.3M;
                            }
                            else if (productName == "Commercial Combined")
                            {
                                if (ELData.GetAttributeValue<int>("lux_el") == 1)
                                {
                                    ELRate = 0.10M;
                                }
                                else if (ELData.GetAttributeValue<int>("lux_el") == 2)
                                {
                                    ELRate = 0.30M;
                                }
                                else if (ELData.GetAttributeValue<int>("lux_el") == 3)
                                {
                                    ELRate = 0.40M;
                                }
                                else if (ELData.GetAttributeValue<int>("lux_el") == 4)
                                {
                                    ELRate = 0.50M;
                                }
                                else if (ELData.GetAttributeValue<int>("lux_el") == 5)
                                {
                                    ELRate = 0.75M;
                                }
                                else if (ELData.GetAttributeValue<int>("lux_el") == 6)
                                {
                                    ELRate = 0.00M;
                                }
                                else
                                {
                                    ELRate = ELData.GetAttributeValue<decimal>("lux_elrate");
                                }
                            }
                            else
                            {
                                ELRate = ELData.GetAttributeValue<decimal>("lux_elrate");
                            }
                        }

                        //var clericalPremium = clerical * Convert.ToDecimal(0.15) / 100; Old Rate
                        var clericalPremium = clerical * Convert.ToDecimal(0.1) / 100;
                        var manualPremium = manual * Convert.ToDecimal(ELRate) / 100;
                        var workAwayPremium = workaway * Convert.ToDecimal(1) / 100;

                        if (inceptionDate >= new DateTime(2023, 11, 01))
                        {
                            clericalPremium = 1.1M * clerical * Convert.ToDecimal(0.1) / 100;
                            manualPremium = 1.1M * manual * Convert.ToDecimal(ELRate) / 100;
                            workAwayPremium = 1.1M * workaway * Convert.ToDecimal(1) / 100;
                        }


                        if (DateTime.UtcNow >= new DateTime(2025, 01, 01))
                        {
                            clericalPremium = 1.06M * clericalPremium;
                            manualPremium = 1.06M * manualPremium;
                            workAwayPremium = 1.06M * workAwayPremium;
                        }


                        TotalELPremium = clericalPremium + manualPremium + workAwayPremium;

                        if (TotalELPremium < 100)
                        {
                            TotalELPremium = 100;
                        }
                        appln["lux_employersliabilitypremium"] = new Money(TotalELPremium);
                        appln["lux_workawaypremium"] = new Money(workAwayPremium);
                        appln["lux_clericalrate"] = Convert.ToDecimal(0.1);
                        //appln["lux_clericalrate"] = Convert.ToDecimal(0.15); Old Rate
                        appln["lux_manualrate"] = ELRate;
                        appln["lux_manualworkawayrate"] = Convert.ToDecimal(1);
                    }
                    else
                    {
                        TotalELPremium = 0;
                        appln["lux_employersliabilitypremium"] = new Money(TotalELPremium);
                        appln["lux_workawaypremium"] = new Money(0);
                        appln["lux_clericalrate"] = Convert.ToDecimal(0);
                        appln["lux_manualrate"] = Convert.ToDecimal(0);
                        appln["lux_manualworkawayrate"] = Convert.ToDecimal(0);
                    }
                }
                else
                {
                    if (appln.GetAttributeValue<bool>("lux_iselcoverrequired") == true)
                    {
                        var clerical = appln.Contains("lux_clericalcommercialandmanagerialwageroll") ? appln.GetAttributeValue<Money>("lux_clericalcommercialandmanagerialwageroll").Value : 0;
                        var siteSupervisor = appln.Contains("lux_sitesupervisorswageroll") ? appln.GetAttributeValue<Money>("lux_sitesupervisorswageroll").Value : 0;

                        var manual = appln.Contains("lux_manualwageroll") ? appln.GetAttributeValue<Money>("lux_manualwageroll").Value : 0;
                        var labourOnly = appln.Contains("lux_workawaywageroll") ? appln.GetAttributeValue<Money>("lux_workawaywageroll").Value : 0;
                        decimal ELRate = 0;

                        if (service.RetrieveMultiple(new FetchExpression(FireRateFetch)).Entities.Count > 0)
                        {
                            var ELData = service.RetrieveMultiple(new FetchExpression(FireRateFetch)).Entities[0];
                            ELRate = ELData.GetAttributeValue<decimal>("lux_elrate");
                        }

                        var clericalPremium = clerical * Convert.ToDecimal(0.1) / 100;
                        var siteSupervisorPremium = siteSupervisor * Convert.ToDecimal(0.5) / 100;

                        var manualPremium = manual * Convert.ToDecimal(ELRate) / 100;
                        var labourOnlyPremium = labourOnly * Convert.ToDecimal(ELRate) / 100;

                        TotalELPremium = clericalPremium + siteSupervisorPremium + manualPremium + labourOnlyPremium;

                        TotalELPremium = TotalELPremium * dateDiffDays / 365;

                        if (inceptionDate >= new DateTime(2023, 11, 01))
                        {
                            TotalELPremium = 1.1M * TotalELPremium;
                        }

                        if (DateTime.UtcNow >= new DateTime(2025, 01, 01))
                        {
                            TotalELPremium = 1.06M * TotalELPremium;
                        }


                        if (TotalELPremium < 250)
                        {
                            TotalELPremium = 250;
                        }
                        appln["lux_employersliabilitypremium"] = new Money(TotalELPremium);
                        appln["lux_clericalrate"] = Convert.ToDecimal(0.1);
                        appln["lux_sitesupervisorrate"] = Convert.ToDecimal(0.5);
                        appln["lux_manualrate"] = ELRate;
                        appln["lux_manualworkawayrate"] = ELRate;
                    }
                    else
                    {
                        TotalELPremium = 0;
                        appln["lux_employersliabilitypremium"] = new Money(TotalELPremium);
                        appln["lux_clericalrate"] = Convert.ToDecimal(0);
                        appln["lux_sitesupervisorrate"] = Convert.ToDecimal(0);
                        appln["lux_manualrate"] = Convert.ToDecimal(0);
                        appln["lux_manualworkawayrate"] = Convert.ToDecimal(0);
                    }
                }


                //PL Premium
                if (productName != "Contractors Combined")
                {
                    decimal PremisePremium = 0;
                    decimal UKPremium = 0;
                    decimal EUPremium = 0;
                    decimal ROWPremium = 0;
                    decimal USPremium = 0;
                    decimal WorkAwayPremium = 0;
                    decimal WorkAwayRate = 0;
                    decimal ProductsRate = 0;
                    if (appln.Attributes.Contains("lux_maintradeforthispremises"))
                    {
                        if (service.RetrieveMultiple(new FetchExpression(FireRateFetch)).Entities.Count > 0)
                        {
                            var FireData = service.RetrieveMultiple(new FetchExpression(FireRateFetch)).Entities[0];
                            if (productName == "Pubs & Restaurants" || productName == "Hotels and Guesthouses")
                            {
                                PremisePremium = 250;
                                ProductsRate = 0.04M;
                                WorkAwayRate = FireData.GetAttributeValue<decimal>("lux_plworkawaywagesrate");
                            }
                            else if (productName == "Retail")
                            {
                                PremisePremium = FireData.GetAttributeValue<decimal>("lux_plpremiserate");
                                if (FireData.GetAttributeValue<int>("lux_prods") >= 1 && FireData.GetAttributeValue<int>("lux_prods") <= 4)
                                {
                                    ProductsRate = 0.02M;
                                }
                                else if (FireData.GetAttributeValue<int>("lux_prods") == 5)
                                {
                                    ProductsRate = 0.06M;
                                }
                                else if (FireData.GetAttributeValue<int>("lux_prods") == 6)
                                {
                                    ProductsRate = 0.00M;
                                }
                                else
                                {
                                    ProductsRate = FireData.GetAttributeValue<decimal>("lux_productsrate");
                                }

                                if (FireData.GetAttributeValue<int>("lux_workaway") < 6)
                                {
                                    WorkAwayRate = 0.4M;
                                }
                                else
                                {
                                    WorkAwayRate = FireData.GetAttributeValue<decimal>("lux_plworkawaywagesrate");
                                }
                            }
                            else if (productName == "Commercial Combined")
                            {
                                WorkAwayRate = FireData.GetAttributeValue<decimal>("lux_plworkawaywagesrate");
                                PremisePremium = FireData.GetAttributeValue<decimal>("lux_plpremiserate");
                                if (FireData.GetAttributeValue<int>("lux_prods") == 1)
                                {
                                    ProductsRate = 0.02M;
                                }
                                else if (FireData.GetAttributeValue<int>("lux_prods") == 2)
                                {
                                    ProductsRate = 0.03M;
                                }
                                else if (FireData.GetAttributeValue<int>("lux_prods") == 3)
                                {
                                    ProductsRate = 0.04M;
                                }
                                else if (FireData.GetAttributeValue<int>("lux_prods") == 4)
                                {
                                    ProductsRate = 0.05M;
                                }
                                else if (FireData.GetAttributeValue<int>("lux_prods") == 5)
                                {
                                    ProductsRate = 0.075M;
                                }
                                else if (FireData.GetAttributeValue<int>("lux_prods") == 6)
                                {
                                    ProductsRate = 0.00M;
                                }
                                else
                                {
                                    ProductsRate = FireData.GetAttributeValue<decimal>("lux_productsrate");
                                }
                            }
                            else
                            {
                                PremisePremium = FireData.GetAttributeValue<decimal>("lux_plpremiserate");
                                ProductsRate = FireData.GetAttributeValue<decimal>("lux_productsrate");
                                WorkAwayRate = FireData.GetAttributeValue<decimal>("lux_plworkawaywagesrate");
                            }
                        }
                    }
                    if (productName == "Commercial Combined")
                    {
                        if (appln.Attributes.Contains("lux_workawaytrade"))
                        {
                            var WorkRateFetch = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                                              <entity name='lux_propertyownersrate'>
                                                <attribute name='lux_workaway' />
                                                <attribute name='lux_transitratesendings' />
                                                <attribute name='lux_transitrateownvehicle' />
                                                <attribute name='lux_tradesegment' />
                                                <attribute name='lux_tradesector' />
                                                <attribute name='lux_theftstockrate' />
                                                <attribute name='lux_theftcontentsrate' />
                                                <attribute name='lux_theftbyemployeetradebaserate' />
                                                <attribute name='lux_theft' />
                                                <attribute name='lux_productsrate' />
                                                <attribute name='lux_prods' />
                                                <attribute name='lux_plworkawaywagesrate' />
                                                <attribute name='lux_plpremiserate' />
                                                <attribute name='lux_mdbi' />
                                                <attribute name='lux_mdfirerate' />
                                                <attribute name='lux_fulldescription' />
                                                <attribute name='lux_elrate' />
                                                <attribute name='lux_blfirerate' />
                                                <attribute name='lux_el' />
                                                <attribute name='lux_propertyownersrateid' />
                                                <order attribute='lux_blfirerate' descending='false' />
                                                <filter type='and'>
                                                  <condition attribute='statecode' operator='eq' value='0' />
                                                  <condition attribute='lux_startdate' operator='on-or-before' value='{String.Format("{0:MM/dd/yyyy}", quotationDate)}' />
                                                  <filter type='or'>
                                                    <condition attribute='lux_enddate' operator='on-or-after' value='{String.Format("{0:MM/dd/yyyy}", quotationDate)}' />
                                                    <condition attribute='lux_enddate' operator='null' />
                                                  </filter>
                                                  <condition attribute='lux_name' operator='eq' uiname='' value='{appln.FormattedValues["lux_workawaytrade"].ToString()}' />
                                                </filter>
                                              </entity>
                                            </fetch>";

                            if (service.RetrieveMultiple(new FetchExpression(WorkRateFetch)).Entities.Count > 0)
                            {
                                var FireData = service.RetrieveMultiple(new FetchExpression(WorkRateFetch)).Entities[0];
                                WorkAwayRate = FireData.GetAttributeValue<decimal>("lux_plworkawaywagesrate");
                            }
                        }
                    }

                    decimal TurnoverRate = ProductsRate;

                    var UKTurnover = appln.Attributes.Contains("lux_ukturnover") ? appln.GetAttributeValue<Money>("lux_ukturnover").Value : 0;
                    var EUTurnover = appln.Attributes.Contains("lux_euturnover") ? appln.GetAttributeValue<Money>("lux_euturnover").Value : 0;
                    var RestOfWorldTurnover = appln.Attributes.Contains("lux_restoftheworldturnover") ? appln.GetAttributeValue<Money>("lux_restoftheworldturnover").Value : 0;
                    var NorthAmericaTurnover = appln.Attributes.Contains("lux_northamericaandcanadaturnover") ? appln.GetAttributeValue<Money>("lux_northamericaandcanadaturnover").Value : 0;

                    UKPremium = UKTurnover * TurnoverRate / 100;
                    EUPremium = EUTurnover * TurnoverRate / 100;
                    ROWPremium = RestOfWorldTurnover * TurnoverRate * Convert.ToDecimal(2.5) / 100;
                    USPremium = NorthAmericaTurnover * TurnoverRate * 10 / 100;

                    var PLLimitofIndemnity = appln.Attributes.Contains("lux_pllimitofindemnity") ? appln.GetAttributeValue<OptionSetValue>("lux_pllimitofindemnity").Value : 0;
                    var Load = 0;
                    if (PLLimitofIndemnity == 972970001)
                    {
                        Load = 0;
                    }
                    else if (PLLimitofIndemnity == 972970002)
                    {
                        Load = 20;
                    }
                    else if (PLLimitofIndemnity == 972970003)
                    {
                        Load = 40;
                    }

                    decimal workaway = 0;
                    decimal bonafide = 0;
                    if (appln.Attributes.Contains("lux_isanyworkawaycarriedoutotherthanforcollec") && appln.GetAttributeValue<bool>("lux_isanyworkawaycarriedoutotherthanforcollec") == true)
                    {
                        workaway = appln.Contains("lux_workawaywageroll") ? appln.GetAttributeValue<Money>("lux_workawaywageroll").Value : 0;
                        bonafide = appln.Contains("lux_bonafidesubcontractorswageroll") ? appln.GetAttributeValue<Money>("lux_bonafidesubcontractorswageroll").Value : 0;
                    }

                    WorkAwayPremium = workaway * WorkAwayRate / 100 + bonafide * (WorkAwayRate * Convert.ToDecimal(0.30)) / 100;

                    if (inceptionDate >= new DateTime(2023, 11, 01))
                    {
                        UKPremium = 1.1M * UKPremium;
                        EUPremium = 1.1M * EUPremium;
                        ROWPremium = 1.1M * ROWPremium;
                        USPremium = 1.1M * USPremium;
                        WorkAwayPremium = 1.1M * WorkAwayPremium;
                        PremisePremium = 1.1M * PremisePremium;
                    }

                    if (DateTime.UtcNow >= new DateTime(2025, 01, 01))
                    {
                        UKPremium = 1.05M * UKPremium;
                        EUPremium = 1.05M * EUPremium;
                        ROWPremium = 1.05M * ROWPremium;
                        USPremium = 1.05M * USPremium;
                        WorkAwayPremium = 1.05M * WorkAwayPremium;
                        PremisePremium = 1.05M * PremisePremium;
                    }

                    TotalPLPremium = UKPremium + EUPremium + ROWPremium + USPremium + WorkAwayPremium + PremisePremium * PremiseCount;

                    TotalPLPremium = TotalPLPremium + TotalPLPremium * Load / 100;


                    if (productName == "Retail")
                    {
                        if (TotalPLPremium < 100)
                        {
                            TotalPLPremium = 100;
                        }
                    }
                    else if (productName == "Commercial Combined")
                    {
                        if (TotalPLPremium < 100)
                        {
                            TotalPLPremium = 100;
                        }
                    }
                    else if (productName == "Pubs & Restaurants" || productName == "Hotels and Guesthouses")
                    {
                        if (TotalPLPremium < 350)
                        {
                            TotalPLPremium = 350;
                        }
                    }
                    appln["lux_publicproductsliabilitypremium"] = new Money(TotalPLPremium);
                    appln["lux_plworkawaypremium"] = new Money(WorkAwayPremium);
                    appln["lux_premisesrate"] = new Money(PremisePremium * PremiseCount);
                    appln["lux_ukeuturnoverrate"] = TurnoverRate;
                    appln["lux_restoftheworldrate"] = TurnoverRate * Convert.ToDecimal(2.5);
                    appln["lux_northamericacanadarate"] = TurnoverRate * Convert.ToDecimal(10);
                    appln["lux_plproductspremiumload"] = Convert.ToDecimal(Load);
                    appln["lux_workawaydirectemployeesrate"] = WorkAwayRate;
                    appln["lux_workawaybonafidesubcontractorsrate"] = WorkAwayRate * Convert.ToDecimal(0.30);
                    if (productName == "Commercial Combined")
                    {
                        if (appln.GetAttributeValue<bool>("lux_ispublicandproductsliabilitycoverrequired") == false)
                        {
                            TotalPLPremium = 0;
                            appln["lux_publicproductsliabilitypremium"] = new Money(TotalPLPremium);
                            appln["lux_plworkawaypremium"] = new Money(0);
                            appln["lux_premisesrate"] = new Money(0);
                            appln["lux_ukeuturnoverrate"] = Convert.ToDecimal(0);
                            appln["lux_restoftheworldrate"] = Convert.ToDecimal(0);
                            appln["lux_northamericacanadarate"] = Convert.ToDecimal(0);
                            appln["lux_plproductspremiumload"] = Convert.ToDecimal(0);
                            appln["lux_workawaydirectemployeesrate"] = Convert.ToDecimal(0);
                            appln["lux_workawaybonafidesubcontractorsrate"] = Convert.ToDecimal(0);
                        }
                    }
                }
                else
                {
                    decimal primaryTradePremium = 0;
                    decimal secondaryTradePremium = 0;
                    decimal primaryTradeRate = 0;
                    decimal secondaryTradeRate = 0;

                    if (appln.Attributes.Contains("lux_contractorsprimarytrade"))
                    {
                        var primaryTrade = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                                                  <entity name='lux_contractorstrade'>
                                                    <attribute name='lux_contractorstradeid' />
                                                    <attribute name='lux_name' />
                                                    <attribute name='createdon' />
                                                    <attribute name='lux_tradecategory' />
                                                    <attribute name='lux_plrate' />
                                                    <attribute name='lux_materialdamagefirerate' />
                                                    <attribute name='lux_elrate' />
                                                    <attribute name='lux_carrate' />
                                                    <attribute name='lux_businessinterruptionrate' />
                                                    <order attribute='lux_name' descending='false' />
                                                    <filter type='and'>
                                                      <condition attribute='statecode' operator='eq' value='0' />
                                                      <condition attribute='lux_startdate' operator='on-or-before' value='{String.Format("{0:MM/dd/yyyy}", quotationDate)}' />
                                                      <filter type='or'>
                                                        <condition attribute='lux_enddate' operator='on-or-after' value='{String.Format("{0:MM/dd/yyyy}", quotationDate)}' />
                                                        <condition attribute='lux_enddate' operator='null' />
                                                      </filter>
                                                      <condition attribute='lux_contractorstradeid' operator='eq' uiname='Aerial Erection' uitype='lux_contractorstrade' value='{appln.GetAttributeValue<EntityReference>("lux_contractorsprimarytrade").Id}' />
                                                    </filter>
                                                  </entity>
                                                </fetch>";

                        if (service.RetrieveMultiple(new FetchExpression(primaryTrade)).Entities.Count > 0)
                        {
                            var PLData = service.RetrieveMultiple(new FetchExpression(primaryTrade)).Entities[0];
                            primaryTradeRate = PLData.GetAttributeValue<decimal>("lux_plrate");
                        }
                    }
                    if (appln.Attributes.Contains("lux_contractorssecondarytrade"))
                    {
                        var secondaryTrade = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                                                  <entity name='lux_contractorstrade'>
                                                    <attribute name='lux_contractorstradeid' />
                                                    <attribute name='lux_name' />
                                                    <attribute name='createdon' />
                                                    <attribute name='lux_tradecategory' />
                                                    <attribute name='lux_plrate' />
                                                    <attribute name='lux_materialdamagefirerate' />
                                                    <attribute name='lux_elrate' />
                                                    <attribute name='lux_carrate' />
                                                    <attribute name='lux_businessinterruptionrate' />
                                                    <order attribute='lux_name' descending='false' />
                                                    <filter type='and'>
                                                      <condition attribute='statecode' operator='eq' value='0' />
                                                      <condition attribute='lux_startdate' operator='on-or-before' value='{String.Format("{0:MM/dd/yyyy}", quotationDate)}' />
                                                      <filter type='or'>
                                                        <condition attribute='lux_enddate' operator='on-or-after' value='{String.Format("{0:MM/dd/yyyy}", quotationDate)}' />
                                                        <condition attribute='lux_enddate' operator='null' />
                                                      </filter>
                                                      <condition attribute='lux_contractorstradeid' operator='eq' uiname='Aerial Erection' uitype='lux_contractorstrade' value='{appln.GetAttributeValue<EntityReference>("lux_contractorssecondarytrade").Id}' />
                                                    </filter>
                                                  </entity>
                                                </fetch>";

                        if (service.RetrieveMultiple(new FetchExpression(secondaryTrade)).Entities.Count > 0)
                        {
                            var PLData = service.RetrieveMultiple(new FetchExpression(secondaryTrade)).Entities[0];
                            secondaryTradeRate = PLData.GetAttributeValue<decimal>("lux_plrate");
                        }
                    }

                    var primaryTurnover = appln.Contains("lux_primarytradeturnover") ? appln.GetAttributeValue<Money>("lux_primarytradeturnover").Value : 0;
                    var secondaryTurnover = appln.Contains("lux_secondarytradeturnover") ? appln.GetAttributeValue<Money>("lux_secondarytradeturnover").Value : 0;
                    var BFSCWageroll = appln.Contains("lux_bonafidesubcontractorswageroll") ? appln.GetAttributeValue<Money>("lux_bonafidesubcontractorswageroll").Value : 0;

                    primaryTradePremium = (primaryTurnover - BFSCWageroll) * primaryTradeRate / 100;
                    secondaryTradePremium = secondaryTurnover * secondaryTradeRate / 100;

                    var bonafidewageroll = appln.Contains("lux_bonafidesubcontractorswageroll") ? appln.GetAttributeValue<Money>("lux_bonafidesubcontractorswageroll").Value : 0;
                    var bfscPremium = bonafidewageroll * Convert.ToDecimal(0.1) / 100;

                    TotalPLPremium = primaryTradePremium + secondaryTradePremium + bfscPremium;

                    var PLLimitofIndemnity = appln.Attributes.Contains("lux_pllimitofindemnity") ? appln.GetAttributeValue<OptionSetValue>("lux_pllimitofindemnity").Value : 0;
                    var Load = 0;
                    if (PLLimitofIndemnity == 972970004)
                    {
                        Load = -30;
                    }
                    else if (PLLimitofIndemnity == 972970001)
                    {
                        Load = -20;
                    }
                    else if (PLLimitofIndemnity == 972970002)
                    {
                        Load = 0;
                    }

                    TotalPLPremium = TotalPLPremium + TotalPLPremium * Load / 100;

                    TotalPLPremium = TotalPLPremium * dateDiffDays / 365;

                    if (inceptionDate >= new DateTime(2023, 11, 01))
                    {
                        TotalPLPremium = 1.1M * TotalPLPremium;
                    }

                    if (DateTime.UtcNow >= new DateTime(2025, 01, 01))
                    {
                        TotalPLPremium = 1.05M * TotalPLPremium;
                    }

                    if (TotalPLPremium < 250)
                    {
                        TotalPLPremium = 250;
                    }

                    appln["lux_publicproductsliabilitypremium"] = new Money(TotalPLPremium);
                    appln["lux_ukeuturnoverrate"] = primaryTradeRate;
                    appln["lux_restoftheworldrate"] = secondaryTradeRate;
                    appln["lux_workawaybonafidesubcontractorsrate"] = Convert.ToDecimal(0.1);
                    if (appln.GetAttributeValue<bool>("lux_ispublicandproductsliabilitycoverrequired") == false)
                    {
                        TotalPLPremium = 0;
                        appln["lux_publicproductsliabilitypremium"] = new Money(TotalPLPremium);
                        appln["lux_ukeuturnoverrate"] = Convert.ToDecimal(0);
                        appln["lux_restoftheworldrate"] = Convert.ToDecimal(0);
                        appln["lux_workawaybonafidesubcontractorsrate"] = Convert.ToDecimal(0);
                    }
                }


                //GIT Premium
                decimal OwnVehicleSumInsured = 0;
                decimal TPSendingSumInsured = 0;
                //decimal TotalSumInsured = 0;
                decimal OwnVehicleRate = 0;
                decimal TPSendingRate = 0;
                if (appln.GetAttributeValue<bool>("lux_goodsintransit") == true)
                {
                    if (appln.GetAttributeValue<OptionSetValueCollection>("lux_gitcoverbasisrequired") != null)
                    {
                        if (appln.Attributes.Contains("lux_maintradeforthispremises"))
                        {
                            if (service.RetrieveMultiple(new FetchExpression(FireRateFetch)).Entities.Count > 0)
                            {
                                var FireData = service.RetrieveMultiple(new FetchExpression(FireRateFetch)).Entities[0];
                                OwnVehicleRate = FireData.GetAttributeValue<decimal>("lux_transitrateownvehicle");
                                TPSendingRate = FireData.GetAttributeValue<decimal>("lux_transitratesendings");
                            }
                        }

                        var coll = appln.GetAttributeValue<OptionSetValueCollection>("lux_gitcoverbasisrequired");
                        foreach (var data in coll)
                        {
                            if (data.Value == 972970001)
                            {
                                var NumberOfOwnVehicles = appln.Attributes.Contains("lux_numberofvehicles") ? appln.GetAttributeValue<int>("lux_numberofvehicles") : 0;
                                var VehicleLimit = appln.Attributes.Contains("lux_maximumsuminsuredpervehicle") ? appln.FormattedValues["lux_maximumsuminsuredpervehicle"].ToString().Replace("£", "") : "0";

                                //old rates
                                //OwnVehicleSumInsured = NumberOfOwnVehicles * Convert.ToDecimal(VehicleLimit) * Convert.ToDecimal(OwnVehicleRate) / 100;

                                OwnVehicleRate = 1;

                                if (NumberOfOwnVehicles == 1)
                                {
                                    OwnVehicleSumInsured = NumberOfOwnVehicles * Convert.ToDecimal(VehicleLimit) * Convert.ToDecimal(OwnVehicleRate) / 100;
                                }
                                else
                                {
                                    if (NumberOfOwnVehicles == 2)
                                    {
                                        OwnVehicleSumInsured = 1 * Convert.ToDecimal(VehicleLimit) * Convert.ToDecimal(OwnVehicleRate) / 100;
                                        var remainingContent = NumberOfOwnVehicles - 1;
                                        OwnVehicleSumInsured += remainingContent * Convert.ToDecimal(VehicleLimit) * (OwnVehicleRate * 75 / 100) / 100;
                                    }
                                    else if (NumberOfOwnVehicles == 3)
                                    {
                                        OwnVehicleSumInsured = 1 * Convert.ToDecimal(VehicleLimit) * Convert.ToDecimal(OwnVehicleRate) / 100;
                                        OwnVehicleSumInsured += 1 * Convert.ToDecimal(VehicleLimit) * (OwnVehicleRate * 75 / 100) / 100;
                                        var remainingContent = NumberOfOwnVehicles - 2;
                                        OwnVehicleSumInsured += remainingContent * Convert.ToDecimal(VehicleLimit) * (OwnVehicleRate * 5 / 100) / 100;
                                    }
                                    else
                                    {
                                        OwnVehicleSumInsured = 1 * Convert.ToDecimal(VehicleLimit) * Convert.ToDecimal(OwnVehicleRate) / 100;
                                        OwnVehicleSumInsured += 1 * Convert.ToDecimal(VehicleLimit) * (OwnVehicleRate * 75 / 100) / 100;
                                        OwnVehicleSumInsured += 1 * Convert.ToDecimal(VehicleLimit) * (OwnVehicleRate * 50 / 100) / 100;
                                        var remainingContent = NumberOfOwnVehicles - 3;
                                        OwnVehicleSumInsured += remainingContent * Convert.ToDecimal(VehicleLimit) * (OwnVehicleRate * 25 / 100) / 100;
                                    }
                                }
                            }
                            else if (data.Value == 972970002)
                            {
                                var AnnualCarryings = appln.Attributes.Contains("lux_annualcarryings") ? appln.GetAttributeValue<Money>("lux_annualcarryings").Value : 0;
                                //old rates
                                //var AnnualCarryingsSI = AnnualCarryings * Convert.ToDecimal(TPSendingRate) / 100;

                                var AnnualCarryingsSI = AnnualCarryings * Convert.ToDecimal(0) / 100;
                                TPSendingSumInsured = AnnualCarryingsSI;
                            }
                        }
                    }
                    TotalGITPremium = OwnVehicleSumInsured + TPSendingSumInsured;
                    TotalGITPremium = TotalGITPremium * dateDiffDays / 365;

                    if (TotalGITPremium < 25)
                    {
                        TotalGITPremium = 25;
                    }
                    appln["lux_goodsintransitpremium"] = new Money(TotalGITPremium);
                    appln["lux_annualcarryingsrate"] = TPSendingRate;
                    appln["lux_vehiclelimitrate"] = OwnVehicleRate;
                }
                else
                {
                    TotalGITPremium = 0;
                    appln["lux_goodsintransitpremium"] = new Money(TotalGITPremium);
                    appln["lux_annualcarryingsrate"] = Convert.ToDecimal(0);
                    appln["lux_vehiclelimitrate"] = Convert.ToDecimal(0);
                }

                if (productName == "Contractors Combined")
                {
                    TotalGITPremium = 0;
                    appln["lux_goodsintransitpremium"] = new Money(TotalGITPremium);
                    appln["lux_annualcarryingsrate"] = Convert.ToDecimal(0);
                    appln["lux_vehiclelimitrate"] = Convert.ToDecimal(0);
                }


                //ALL Risk Premium
                var ARfetch = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                              <entity name='lux_allriskitem'>
                                <attribute name='lux_typeofequipment' />
                                <attribute name='lux_territoriallimit' />
                                <attribute name='lux_suminsured' />
                                <attribute name='lux_excess' />
                                <attribute name='lux_allriskitemid' />
                                <order attribute='lux_typeofequipment' descending='false' />
                                <filter type='and'>
                                  <condition attribute='statecode' operator='eq' value='0' />
                                  <condition attribute='lux_application' operator='eq' uiname='' uitype='lux_propertyownersapplications' value='{appln.Id}' />
                                </filter>
                                <link-entity name='lux_propertyownersapplications' from='lux_propertyownersapplicationsid' to='lux_application' link-type='inner' alias='aa'>
                                  <filter type='and'>
                                    <condition attribute='lux_allriskitems' operator='eq' value='1' />
                                  </filter>
                                </link-entity>
                              </entity>
                            </fetch>";

                if (service.RetrieveMultiple(new FetchExpression(ARfetch)).Entities.Count > 0)
                {
                    foreach (var item in service.RetrieveMultiple(new FetchExpression(ARfetch)).Entities)
                    {
                        var SARFetch = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                                      <entity name='lux_specifiedallrisksrate'>
                                        <attribute name='lux_name' />
                                        <attribute name='createdon' />
                                        <attribute name='lux_rate' />
                                        <attribute name='lux_specifiedallrisksrateid' />
                                        <order attribute='lux_name' descending='false' />
                                        <filter type='and'>
                                          <condition attribute='statecode' operator='eq' value='0' />
                                          <condition attribute='lux_specifiedallrisks' operator='eq' value='{item.GetAttributeValue<OptionSetValue>("lux_typeofequipment").Value}' />
                                        </filter>
                                      </entity>
                                    </fetch>";

                        if (service.RetrieveMultiple(new FetchExpression(SARFetch)).Entities.Count > 0)
                        {
                            var data = service.RetrieveMultiple(new FetchExpression(SARFetch)).Entities[0];
                            var Rate = data.GetAttributeValue<decimal>("lux_rate");
                            var SumInsured = item.Attributes.Contains("lux_suminsured") ? item.GetAttributeValue<Money>("lux_suminsured").Value : 0;
                            TotalAllRiskPremium += SumInsured * Rate / 100;
                        }
                    }

                    TotalAllRiskPremium = TotalAllRiskPremium * dateDiffDays / 365;

                    if (TotalAllRiskPremium < 25)
                    {
                        TotalAllRiskPremium = 25;
                    }
                    appln["lux_specifiedallriskpremium"] = new Money(TotalAllRiskPremium);
                }
                else
                {
                    TotalAllRiskPremium = 0;
                    appln["lux_specifiedallriskpremium"] = new Money(TotalAllRiskPremium);
                }

                if (productName == "Contractors Combined")
                {
                    if (appln.GetAttributeValue<bool>("lux_allriskitems") == false || appln.GetAttributeValue<bool>("lux_ismaterialdamagecoverrequired") == false)
                    {
                        TotalAllRiskPremium = 0;
                        appln["lux_specifiedallriskpremium"] = new Money(TotalAllRiskPremium);
                    }
                }


                //Money Premium
                decimal EstimatedAnnualCaryings = appln.Attributes.Contains("lux_estimatedannualcarryings") ? appln.GetAttributeValue<Money>("lux_estimatedannualcarryings").Value : 0;

                decimal Safe1 = appln.Attributes.Contains("lux_moneyinspecifiedsafe1") ? appln.GetAttributeValue<Money>("lux_moneyinspecifiedsafe1").Value : 0;
                decimal Safe2 = appln.Attributes.Contains("lux_moneyinspecifiedsafe2") ? appln.GetAttributeValue<Money>("lux_moneyinspecifiedsafe2").Value : 0;
                decimal Safe3 = appln.Attributes.Contains("lux_moneyinspecifiedsafe3") ? appln.GetAttributeValue<Money>("lux_moneyinspecifiedsafe3").Value : 0;
                decimal Safe4 = appln.Attributes.Contains("lux_moneyinspecifiedsafe4") ? appln.GetAttributeValue<Money>("lux_moneyinspecifiedsafe4").Value : 0;
                decimal Safe5 = appln.Attributes.Contains("lux_moneyinspecifiedsafe5") ? appln.GetAttributeValue<Money>("lux_moneyinspecifiedsafe5").Value : 0;

                decimal Estimatedannualcarryings = EstimatedAnnualCaryings; // MoneyDuringHours + MoneyInTransit + MoneyOutofSafe + MoneyOutofHours;
                decimal MoneyinSafe = Safe1 + Safe2 + Safe3 + Safe4 + Safe5;

                //old rates
                //var EstimatedannualcarryingsPremium = Estimatedannualcarryings * Convert.ToDecimal(0.05) / 100;
                //var MoneyinSafePremium = MoneyinSafe * Convert.ToDecimal(1.5) / 100;

                var EstimatedannualcarryingsPremium = Estimatedannualcarryings * Convert.ToDecimal(0.00) / 100;
                var MoneyinSafePremium = MoneyinSafe * Convert.ToDecimal(1) / 100;

                TotalMoneyPremium = EstimatedannualcarryingsPremium + MoneyinSafePremium;

                TotalMoneyPremium = TotalMoneyPremium * dateDiffDays / 365;

                if (TotalMoneyPremium < 25)
                {
                    TotalMoneyPremium = 25;
                }
                appln["lux_retailmoneypremium"] = new Money(TotalMoneyPremium);
                appln["lux_estimatedannualcarryingsrate"] = Convert.ToDecimal(0.00);
                appln["lux_moneyinsaferate"] = Convert.ToDecimal(1);

                if (productName == "Commercial Combined")
                {
                    if (appln.GetAttributeValue<bool>("lux_ismoneycoverrequired") == false)
                    {
                        TotalMoneyPremium = 0;
                        appln["lux_retailmoneypremium"] = new Money(TotalMoneyPremium);
                        appln["lux_estimatedannualcarryingsrate"] = Convert.ToDecimal(0);
                        appln["lux_moneyinsaferate"] = Convert.ToDecimal(0);
                    }
                }
                if (productName == "Contractors Combined")
                {
                    if (appln.GetAttributeValue<bool>("lux_ismoneycoverrequired") == false || appln.GetAttributeValue<bool>("lux_ismaterialdamagecoverrequired") == false)
                    {
                        TotalMoneyPremium = 0;
                        appln["lux_retailmoneypremium"] = new Money(TotalMoneyPremium);
                        appln["lux_estimatedannualcarryingsrate"] = Convert.ToDecimal(0);
                        appln["lux_moneyinsaferate"] = Convert.ToDecimal(0);
                    }
                }


                // Contracts Work Premium
                if (productName == "Contractors Combined")
                {
                    if (appln.GetAttributeValue<bool>("lux_doyourequirecoverforcontractwork") == true)
                    {
                        decimal OwnPlantandTools = appln.Attributes.Contains("lux_ownplanttoolsandequipmenttotalreplacement") ? appln.GetAttributeValue<Money>("lux_ownplanttoolsandequipmenttotalreplacement").Value : 0;
                        decimal OwnTemporaryBuildings = appln.Attributes.Contains("lux_owntemporarybuildingssuminsured") ? appln.GetAttributeValue<Money>("lux_owntemporarybuildingssuminsured").Value : 0;
                        decimal EmployeesTools = appln.Attributes.Contains("lux_employeestoolstotalsuminsured") ? appln.GetAttributeValue<Money>("lux_employeestoolstotalsuminsured").Value : 0;
                        decimal HiredinPlantCharges = appln.Attributes.Contains("lux_hiredinplantcharges") ? appln.GetAttributeValue<Money>("lux_hiredinplantcharges").Value : 0;
                        decimal HiredinTemporaryBuildings = appln.Attributes.Contains("lux_hiredintemporarybuildingssuminsured") ? appln.GetAttributeValue<Money>("lux_hiredintemporarybuildingssuminsured").Value : 0;

                        decimal primaryTurnover = appln.Contains("lux_primarytradeturnover") ? appln.GetAttributeValue<Money>("lux_primarytradeturnover").Value : 0;
                        decimal secondaryTurnover = appln.Contains("lux_secondarytradeturnover") ? appln.GetAttributeValue<Money>("lux_secondarytradeturnover").Value : 0;
                        decimal totalTurnover = primaryTurnover + secondaryTurnover;
                        decimal primaryTradeRate = 0;

                        if (appln.Attributes.Contains("lux_contractorsprimarytrade"))
                        {
                            var primaryTrade = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                                                  <entity name='lux_contractorstrade'>
                                                    <attribute name='lux_contractorstradeid' />
                                                    <attribute name='lux_name' />
                                                    <attribute name='createdon' />
                                                    <attribute name='lux_tradecategory' />
                                                    <attribute name='lux_plrate' />
                                                    <attribute name='lux_materialdamagefirerate' />
                                                    <attribute name='lux_elrate' />
                                                    <attribute name='lux_carrate' />
                                                    <attribute name='lux_businessinterruptionrate' />
                                                    <order attribute='lux_name' descending='false' />
                                                    <filter type='and'>
                                                      <condition attribute='statecode' operator='eq' value='0' />
                                                      <condition attribute='lux_startdate' operator='on-or-before' value='{String.Format("{0:MM/dd/yyyy}", quotationDate)}' />
                                                      <filter type='or'>
                                                        <condition attribute='lux_enddate' operator='on-or-after' value='{String.Format("{0:MM/dd/yyyy}", quotationDate)}' />
                                                        <condition attribute='lux_enddate' operator='null' />
                                                      </filter>
                                                      <condition attribute='lux_contractorstradeid' operator='eq' uiname='Aerial Erection' uitype='lux_contractorstrade' value='{appln.GetAttributeValue<EntityReference>("lux_contractorsprimarytrade").Id}' />
                                                    </filter>
                                                  </entity>
                                                </fetch>";

                            if (service.RetrieveMultiple(new FetchExpression(primaryTrade)).Entities.Count > 0)
                            {
                                var PLData = service.RetrieveMultiple(new FetchExpression(primaryTrade)).Entities[0];
                                primaryTradeRate = PLData.GetAttributeValue<decimal>("lux_carrate");
                            }
                        }

                        decimal CARTurnoverPremium = totalTurnover * primaryTradeRate / 100;

                        decimal ownplantpremium = OwnPlantandTools * Convert.ToDecimal(1.750) / 100;
                        decimal owntemporarybuildingpremium = OwnTemporaryBuildings * Convert.ToDecimal(1.750) / 100;
                        decimal employeetoolspremium = EmployeesTools * Convert.ToDecimal(2) / 100;
                        decimal hiredinplantpremium = HiredinPlantCharges * Convert.ToDecimal(2) / 100;
                        decimal hiredintemporarybuildingpremium = HiredinTemporaryBuildings * Convert.ToDecimal(2.5) / 100;

                        decimal totalContractPlantPremium = ownplantpremium + owntemporarybuildingpremium + employeetoolspremium + hiredinplantpremium + hiredintemporarybuildingpremium;

                        TotalCARPremium = CARTurnoverPremium + totalContractPlantPremium;

                        TotalCARPremium = TotalCARPremium * dateDiffDays / 365;

                        var SizeDiscountFetch = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                                          <entity name='lux_sizediscount'>
                                            <attribute name='lux_effectiveto' />
                                            <attribute name='lux_effectivefrom' />
                                            <attribute name='lux_below500k' />
                                            <attribute name='lux_above5m' />
                                            <attribute name='lux_501k1m' />
                                            <attribute name='lux_20000015m' />
                                            <attribute name='lux_10000012m' />
                                            <order attribute='createdon' descending='false' />
                                            <filter type='and'>
                                              <condition attribute='statecode' operator='eq' value='0' />
                                              <filter type='or'>
                                                <condition attribute='lux_effectivefrom' operator='on-or-before' value='{String.Format("{0:MM/dd/yyyy}", quotationDate)}' />
                                                <condition attribute='lux_effectivefrom' operator='null' />
                                              </filter>
                                              <filter type='or'>
                                                <condition attribute='lux_effectiveto' operator='on-or-after' value= '{String.Format("{0:MM/dd/yyyy}", quotationDate)}' />
                                                <condition attribute='lux_effectiveto' operator='null' />
                                              </filter>
                                              <condition attribute='lux_section' operator='eq' value='972970001' />
                                              <condition attribute='lux_product' operator='eq' uiname='' uitype='product' value='{productData.Id}' />
                                            </filter>
                                          </entity>
                                        </fetch>";

                        if (service.RetrieveMultiple(new FetchExpression(SizeDiscountFetch)).Entities.Count > 0)
                        {
                            var SI_data = service.RetrieveMultiple(new FetchExpression(SizeDiscountFetch)).Entities;
                            var SI_field = "";
                            if (totalTurnover <= 500000)
                            {
                                SI_field = "lux_below500k";
                            }
                            else if (totalTurnover > 500000 && totalTurnover <= 1000000)
                            {
                                SI_field = "lux_501k1m";
                            }
                            else if (totalTurnover > 1000000 && totalTurnover <= 2000000)
                            {
                                SI_field = "lux_10000012m";
                            }
                            else if (totalTurnover > 2000000 && totalTurnover <= 5000000)
                            {
                                SI_field = "lux_20000015m";
                            }
                            else if (totalTurnover > 5000000)
                            {
                                SI_field = "lux_above5m";
                            }
                            var discount = SI_data.FirstOrDefault().GetAttributeValue<decimal>(SI_field);
                            appln["lux_workawayaction"] = discount.ToString("#.##") + "%";

                            if (discount.ToString("#.##") == "")
                            {
                                appln["lux_workawayaction"] = "0%";
                            }

                            TotalCARPremium = TotalCARPremium + TotalCARPremium * discount / 100;
                            CARTurnoverPremium = CARTurnoverPremium + CARTurnoverPremium * discount / 100;
                            totalContractPlantPremium = totalContractPlantPremium + totalContractPlantPremium * discount / 100;
                        }

                        if (TotalCARPremium < 350)
                        {
                            appln["lux_contractorspremium"] = new Money(350);
                        }
                        else
                        {
                            appln["lux_contractorspremium"] = new Money(TotalCARPremium);
                        }
                        appln["lux_contractorsturnoverpremium"] = new Money(CARTurnoverPremium * dateDiffDays / 365);
                        appln["lux_contractorsplantpremium"] = new Money(totalContractPlantPremium * dateDiffDays / 365);
                    }
                    else
                    {
                        appln["lux_contractorspremium"] = new Money(0);
                        appln["lux_contractorsturnoverpremium"] = new Money(0);
                        appln["lux_contractorsplantpremium"] = new Money(0);
                    }
                }

                //ARAG Premium
                if (productData.Attributes["name"].ToString() != "Commercial Combined" && productData.Attributes["name"].ToString() != "Contractors Combined")
                {
                    var aragfetch = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                                          <entity name='lux_aragrate'>
                                            <attribute name='createdon' />
                                            <attribute name='lux_product' />
                                            <attribute name='lux_netrate' />
                                            <attribute name='lux_grossrate' />                                
                                            <attribute name='lux_turnoverfrom' />
                                            <attribute name='lux_turnoverto' />
                                            <attribute name='lux_aragrateid' />
                                            <order attribute='createdon' descending='false' />
                                            <filter type='and'>
                                              <condition attribute='statecode' operator='eq' value='0' />
                                              <condition attribute='lux_product' operator='eq' uiname='' uitype='product' value='{productData.Id}' />
                                              <condition attribute='lux_startdate' operator='on-or-before' value='{String.Format("{0:MM/dd/yyyy}", inceptionDate)}' />
                                              <filter type='or'>
                                                  <condition attribute='lux_enddate' operator='on-or-after' value='{String.Format("{0:MM/dd/yyyy}", inceptionDate)}' />
                                                  <condition attribute='lux_enddate' operator='null' />
                                              </filter>
                                            </filter>
                                          </entity>
                                        </fetch>";


                    if (service.RetrieveMultiple(new FetchExpression(aragfetch)).Entities.Count > 0)
                    {
                        var Rates = service.RetrieveMultiple(new FetchExpression(aragfetch)).Entities[0];

                        TotalLEGrossPremium = Rates.GetAttributeValue<Money>("lux_grossrate").Value * dateDiffDays / 365;
                        TotalLENetPremium = Rates.GetAttributeValue<Money>("lux_netrate").Value * dateDiffDays / 365;

                        appln["lux_legrosspremium"] = new Money(TotalLEGrossPremium);
                        appln["lux_lenetpremium"] = new Money(TotalLENetPremium);
                    }
                }
                else
                {
                    decimal Turnover = 0;
                    if (productData.Attributes["name"].ToString() == "Commercial Combined")
                    {
                        Turnover = appln.Attributes.Contains("lux_turnover") ? appln.GetAttributeValue<Money>("lux_turnover").Value : 0;
                    }
                    else if (productData.Attributes["name"].ToString() == "Contractors Combined")
                    {
                        decimal primaryTurnover = appln.Contains("lux_primarytradeturnover") ? appln.GetAttributeValue<Money>("lux_primarytradeturnover").Value : 0;
                        decimal secondaryTurnover = appln.Contains("lux_secondarytradeturnover") ? appln.GetAttributeValue<Money>("lux_secondarytradeturnover").Value : 0;
                        Turnover = primaryTurnover + secondaryTurnover;
                    }

                    var aragfetch = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                                  <entity name='lux_aragrate'>
                                    <attribute name='createdon' />
                                    <attribute name='lux_product' />
                                    <attribute name='lux_netrate' />
                                    <attribute name='lux_grossrate' />
                                    <attribute name='lux_aragrateid' />
                                    <order attribute='createdon' descending='false' />
                                    <filter type='and'>
                                      <condition attribute='statecode' operator='eq' value='0' />
                                      <condition attribute='lux_product' operator='eq' uiname='' uitype='product' value='{productData.Id}' />
                                      <filter type='or'>
                                        <filter type='and'>
                                          <condition attribute='lux_turnoverfrom' operator='le' value='{Turnover}' />
                                          <condition attribute='lux_turnoverto' operator='ge' value='{Turnover}' />
                                        </filter>
                                        <filter type='and'>
                                          <condition attribute='lux_turnoverfrom' operator='le' value='{Turnover}' />
                                          <condition attribute='lux_turnoverto' operator='null' />
                                        </filter>
                                      </filter>
                                      <condition attribute='lux_startdate' operator='on-or-before' value='{String.Format("{0:MM/dd/yyyy}", inceptionDate)}' />
                                      <filter type='or'>
                                          <condition attribute='lux_enddate' operator='on-or-after' value='{String.Format("{0:MM/dd/yyyy}", inceptionDate)}' />
                                          <condition attribute='lux_enddate' operator='null' />
                                      </filter>
                                    </filter>
                                  </entity>
                                </fetch>";

                    if (service.RetrieveMultiple(new FetchExpression(aragfetch)).Entities.Count > 0)
                    {
                        var Rates = service.RetrieveMultiple(new FetchExpression(aragfetch)).Entities[0];

                        TotalLEGrossPremium = Rates.GetAttributeValue<Money>("lux_grossrate").Value * dateDiffDays / 365;
                        TotalLENetPremium = Rates.GetAttributeValue<Money>("lux_netrate").Value * dateDiffDays / 365;

                        appln["lux_legrosspremium"] = new Money(TotalLEGrossPremium);
                        appln["lux_lenetpremium"] = new Money(TotalLENetPremium);
                    }
                }


                //TotalPremium
                bool IsLocationalLimit = appln.Contains("lux_islocationallimitrequired") ? appln.GetAttributeValue<bool>("lux_islocationallimitrequired") : false;
                decimal MaximumLocationalLimit = appln.Contains("lux_maximumlocationallimit") ? appln.GetAttributeValue<Money>("lux_maximumlocationallimit").Value : 0;

                foreach (var item1 in premises)
                {
                    decimal TotalMDSumInsured = item1.Contains("lux_totalmdsuminsuredwithupliftedamount") ? item1.GetAttributeValue<Money>("lux_totalmdsuminsuredwithupliftedamount").Value : 0;
                    TotalBISumInsured = appln.Contains("lux_totalbisuminsured") ? appln.GetAttributeValue<Money>("lux_totalbisuminsured").Value : 0;
                    Entity premise = service.Retrieve(entityName, item1.Id, new ColumnSet());
                    premise["lux_bisuminsured"] = new Money(TotalBISumInsured);

                    if (IsLocationalLimit == true)
                    {
                        TotalBISumInsured = MaximumLocationalLimit;
                    }
                    premise["lux_totalsuminsured"] = new Money(TotalMDSumInsured + TotalBISumInsured);
                    premise["lux_maximumlocationallimit"] = new Money(MaximumLocationalLimit);
                    service.Update(premise);
                }

                if (productName == "Contractors Combined")
                {
                    if (appln.GetAttributeValue<bool>("lux_ismaterialdamagecoverrequired") == false)
                    {
                        TotalMDPremium = 0;
                        appln["lux_retailmdpremium"] = new Money(0);
                        appln["lux_buildingpremium"] = new Money(0);
                        appln["lux_contentspremium"] = new Money(0);
                        appln["lux_tenentspremium"] = new Money(0);
                        appln["lux_stockpremium"] = Convert.ToDecimal(0);
                        appln["lux_targetstockpremium"] = Convert.ToDecimal(0);
                        appln["lux_computerequipmentpremium"] = Convert.ToDecimal(0);
                        appln["lux_lossofrentpremium"] = Convert.ToDecimal(0);
                        appln["lux_buildingfirerate"] = Convert.ToDecimal(0);
                        appln["lux_buildingperilsrate"] = Convert.ToDecimal(0);
                        appln["lux_contentsfirerate"] = Convert.ToDecimal(0);
                        appln["lux_contentsperilsrate"] = Convert.ToDecimal(0);
                        appln["lux_stockrate"] = Convert.ToDecimal(0);
                        appln["lux_targetstockrate"] = Convert.ToDecimal(0);
                        appln["lux_computerequipmentrate"] = Convert.ToDecimal(0);
                        appln["lux_lossofrentrate"] = Convert.ToDecimal(0);
                    }
                    if (appln.GetAttributeValue<bool>("lux_isbusinessinterruptioncoverrequired") == false || appln.GetAttributeValue<bool>("lux_ismaterialdamagecoverrequired") == false)
                    {
                        TotalBIPremium = 0;
                        appln["lux_retailbipremium"] = new Money(0);
                        appln["lux_totalbisuminsured"] = new Money(0);
                        appln["lux_grossprofitorrevenuerate"] = Convert.ToDecimal(0);
                        appln["lux_grossprofitorrevenuepremium"] = new Money(0);
                        appln["lux_increasedcostofworkingrate"] = Convert.ToDecimal(0);
                        appln["lux_increasedcostofworkingpremium"] = new Money(0);
                        appln["lux_additionalincreasedcostofworkingrate"] = Convert.ToDecimal(0);
                        appln["lux_additionalincreasedcostofworkingpremium"] = new Money(0);
                        appln["lux_bilossofrentrate"] = Convert.ToDecimal(0);
                        appln["lux_bilossofrentpremium"] = new Money(0);
                    }
                }

                var TotalPropertyPremium = TotalMDPremium + TotalBIPremium + TotalMoneyPremium + TotalAllRiskPremium + TotalGITPremium;
                var TotalLiabilityPremium = TotalELPremium + TotalPLPremium + TotalCARPremium;
                var TotalPremium = TotalPropertyPremium + TotalLiabilityPremium;

                appln["lux_totalpremium"] = new Money(TotalPremium + TotalLEGrossPremium);
                appln["lux_brokercommission"] = Convert.ToDouble(BrokerComm) + "%";
                appln["lux_aciestechnicalcommission"] = Convert.ToDouble(aciesComm) + "%";
                var TotalTechComm = Convert.ToDouble(BrokerComm) + Convert.ToDouble(aciesComm);
                appln["lux_totaltechnicalcommission"] = TotalTechComm + "%";

                var LEBrokerComm = TotalLEGrossPremium * BrokerComm / 100;
                var LeGrossComm = TotalLEGrossPremium - TotalLENetPremium - LEBrokerComm;

                appln["lux_brokercommissionamount"] = new Money(TotalPremium * BrokerComm / 100 + LEBrokerComm);
                appln["lux_legrosscommission"] = new Money(LeGrossComm);

                appln["lux_aciestechnicalcommissionamount"] = new Money(TotalPremium * aciesComm / 100);
                appln["lux_originaltechnicalpremium"] = new Money(TotalPremium + TotalLENetPremium - TotalPremium * BrokerComm / 100 - TotalPremium * aciesComm / 100);
                appln["lux_technicalnetpremium"] = new Money(TotalPremium + TotalLENetPremium - TotalPremium * BrokerComm / 100 - TotalPremium * aciesComm / 100);

                if (inceptionDate >= new DateTime(2023, 11, 01))
                {
                    appln["lux_aciestechnicalcommissionamount"] = new Money(TotalPropertyPremium * aciesComm / 100 + TotalLiabilityPremium * LiabilityaciesComm / 100);
                    appln["lux_originaltechnicalpremium"] = new Money(TotalPremium + TotalLENetPremium - TotalPremium * BrokerComm / 100 - TotalPropertyPremium * aciesComm / 100 - TotalLiabilityPremium * LiabilityaciesComm / 100);
                    appln["lux_technicalnetpremium"] = new Money(TotalPremium + TotalLENetPremium - TotalPremium * BrokerComm / 100 - TotalPropertyPremium * aciesComm / 100 - TotalLiabilityPremium * LiabilityaciesComm / 100);
                    appln["lux_aciestechnicalcommissionamountliability"] = new Money(TotalLiabilityPremium * LiabilityaciesComm / 100);
                    appln["lux_aciestechnicalcommissionliability"] = Convert.ToDouble(LiabilityaciesComm) + "%";
                }

                decimal commercialLoadDiscount = appln.Attributes.Contains("lux_commercialloaddiscount") ? appln.GetAttributeValue<decimal>("lux_commercialloaddiscount") : -25;
                commercialLoadDiscount = Broker.Attributes.Contains("lux_discount") ? Broker.GetAttributeValue<decimal>("lux_discount") : commercialLoadDiscount;

                decimal MDSectionDiscount = appln.Attributes.Contains("lux_materialdamagesectiondiscount") ? appln.GetAttributeValue<decimal>("lux_materialdamagesectiondiscount") : commercialLoadDiscount;
                decimal BISectionDiscount = appln.Attributes.Contains("lux_businessinterruptionsectionadjustment") ? appln.GetAttributeValue<decimal>("lux_businessinterruptionsectionadjustment") : commercialLoadDiscount;
                decimal ELSectionDiscount = appln.Attributes.Contains("lux_employersliabilitysectiondiscount") ? appln.GetAttributeValue<decimal>("lux_employersliabilitysectiondiscount") : commercialLoadDiscount;
                decimal PLSectionDiscount = appln.Attributes.Contains("lux_publicproductsliabilitysectiondiscount") ? appln.GetAttributeValue<decimal>("lux_publicproductsliabilitysectiondiscount") : commercialLoadDiscount;
                decimal CWSectionDiscount = appln.Attributes.Contains("lux_contractworkssectiondiscount") ? appln.GetAttributeValue<decimal>("lux_contractworkssectiondiscount") : commercialLoadDiscount;
                decimal LESectionDiscount = 0;

                if (!appln.Attributes.Contains("lux_commercialloaddiscount") || appln.GetAttributeValue<decimal>("lux_commercialloaddiscount") == 0)
                {
                    commercialLoadDiscount = -25;
                    if (IsLive == false)
                    {
                        commercialLoadDiscount = -20;
                    }

                    if (IsLive == true)
                    {
                        var LoadDiscountFetch = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                                                  <entity name='lux_lux_quoteandbindloaddiscount'>
                                                    <attribute name='createdon' />
                                                    <attribute name='lux_validuntil' />
                                                    <attribute name='lux_effectivedate' />
                                                    <attribute name='lux_commercialloaddiscount' />
                                                    <attribute name='lux_product' />
                                                    <attribute name='lux_lux_quoteandbindloaddiscountid' />
                                                    <order attribute='lux_commercialloaddiscount' descending='false' />
                                                    <filter type='and'>
                                                      <condition attribute='statecode' operator='eq' value='0' />
                                                      <condition attribute='lux_product' operator='eq' uiname='' uitype='product' value='{productData.Id}' />
                                                      <condition attribute='lux_effectivedate' operator='on-or-before' value='{String.Format("{0:MM/dd/yyyy}", DateTime.UtcNow)}' />
                                                      <filter type='or'>
                                                        <condition attribute='lux_validuntil' operator='on-or-after' value='{String.Format("{0:MM/dd/yyyy}", DateTime.UtcNow)}' />
                                                        <condition attribute='lux_validuntil' operator='null' />
                                                      </filter>
                                                    </filter>
                                                  </entity>
                                                </fetch>";

                        var loadDiscData = service.RetrieveMultiple(new FetchExpression(LoadDiscountFetch)).Entities;

                        if (loadDiscData.Count > 0)
                        {
                            commercialLoadDiscount = loadDiscData.FirstOrDefault().GetAttributeValue<decimal>("lux_commercialloaddiscount");
                        }

                        if (TradeName == "Fast Food Retailing" || TradeName == "Fast food delivery Service" || TradeName == "Pizza Delivery" || TradeName == "Fish and Chip Shop" || TradeName == "Burger Bars" || TradeName == "Cafe" || TradeName == "Take away food supplier")
                        {
                            var expectedPoloicyPremium = TotalPremium - TotalPremium * commercialLoadDiscount / 100;
                            if (expectedPoloicyPremium < 750)
                            {
                                expectedPoloicyPremium = 750;
                                commercialLoadDiscount = (expectedPoloicyPremium * 100 - TotalPremium * 100) / TotalPremium;
                            }
                        }
                    }

                    MDSectionDiscount = commercialLoadDiscount;
                    BISectionDiscount = commercialLoadDiscount;
                    ELSectionDiscount = commercialLoadDiscount;
                    PLSectionDiscount = commercialLoadDiscount;
                    CWSectionDiscount = commercialLoadDiscount;
                    LESectionDiscount = 0M;
                }


                appln["lux_commercialloaddiscount"] = commercialLoadDiscount;
                appln["lux_materialdamagesectiondiscount"] = MDSectionDiscount;
                appln["lux_businessinterruptionsectionadjustment"] = BISectionDiscount;
                appln["lux_employersliabilitysectiondiscount"] = ELSectionDiscount;
                appln["lux_publicproductsliabilitysectiondiscount"] = PLSectionDiscount;
                appln["lux_contractworkssectiondiscount"] = CWSectionDiscount;
                appln["lux_legalexpensessectiondiscount"] = LESectionDiscount;

                TotalMDPremium = TotalMDPremium + TotalMDPremium * MDSectionDiscount / 100;
                if (TotalMDPremium <= 100)
                {
                    TotalMDPremium = 100;
                }

                var BuildingPremium1 = appln.Attributes.Contains("lux_buildingpremium") ? appln.GetAttributeValue<Money>("lux_buildingpremium").Value : 0;
                BuildingPremium1 = BuildingPremium1 + BuildingPremium1 * MDSectionDiscount / 100;
                var BuildingNetPremium = BuildingPremium1 - BuildingPremium1 * totaltechnicalcommission / 100;
                var BuildingPolicyPremium = BuildingNetPremium / (1 - totalpolicycommission / 100);
                appln["lux_buildingpolicypremium"] = new Money(BuildingPolicyPremium);

                var ContentsPremium1 = appln.Attributes.Contains("lux_contentspremium") ? appln.GetAttributeValue<Money>("lux_contentspremium").Value : 0;
                ContentsPremium1 = ContentsPremium1 + ContentsPremium1 * MDSectionDiscount / 100;
                var ContentsNetPremium = ContentsPremium1 - ContentsPremium1 * totaltechnicalcommission / 100;
                var ContentsPolicyPremium = ContentsNetPremium / (1 - totalpolicycommission / 100);
                appln["lux_contentspolicypremium"] = new Money(ContentsPolicyPremium);

                var TenentsPremium1 = appln.Attributes.Contains("lux_tenentspremium") ? appln.GetAttributeValue<Money>("lux_tenentspremium").Value : 0;
                TenentsPremium1 = TenentsPremium1 + TenentsPremium1 * MDSectionDiscount / 100;
                var TenentsNetPremium = TenentsPremium1 - TenentsPremium1 * totaltechnicalcommission / 100;
                var TenentsPolicyPremium = TenentsNetPremium / (1 - totalpolicycommission / 100);
                appln["lux_tenentspolicypremium"] = new Money(TenentsPolicyPremium);

                var StockPremium1 = appln.Attributes.Contains("lux_stockpremium") ? appln.GetAttributeValue<decimal>("lux_stockpremium") : 0;
                StockPremium1 = StockPremium1 + StockPremium1 * MDSectionDiscount / 100;
                var StockNetPremium = StockPremium1 - StockPremium1 * totaltechnicalcommission / 100;
                var StockPolicyPremium = StockNetPremium / (1 - totalpolicycommission / 100);
                appln["lux_stockpolicypremium"] = new Money(StockPolicyPremium);

                var TargetStockPremium1 = appln.Attributes.Contains("lux_targetstockpremium") ? appln.GetAttributeValue<decimal>("lux_targetstockpremium") : 0;
                TargetStockPremium1 = TargetStockPremium1 + TargetStockPremium1 * MDSectionDiscount / 100;
                var TargetStockNetPremium = TargetStockPremium1 - TargetStockPremium1 * totaltechnicalcommission / 100;
                var TargetStockPolicyPremium = TargetStockNetPremium / (1 - totalpolicycommission / 100);
                appln["lux_targetstockpolicypremium"] = new Money(TargetStockPolicyPremium);

                var ComputerPremium1 = appln.Attributes.Contains("lux_computerequipmentpremium") ? appln.GetAttributeValue<decimal>("lux_computerequipmentpremium") : 0;
                ComputerPremium1 = ComputerPremium1 + ComputerPremium1 * MDSectionDiscount / 100;
                var ComputerPremiumNetPremium = ComputerPremium1 - ComputerPremium1 * totaltechnicalcommission / 100;
                var ComputerPremiumPolicyPremium = ComputerPremiumNetPremium / (1 - totalpolicycommission / 100);
                appln["lux_computerequipmentpolicypremium"] = new Money(ComputerPremiumPolicyPremium);

                var LORPremium1 = appln.Attributes.Contains("lux_lossofrentpremium") ? appln.GetAttributeValue<decimal>("lux_lossofrentpremium") : 0;
                LORPremium1 = LORPremium1 + LORPremium1 * MDSectionDiscount / 100;
                var TargetLORNetPremium = LORPremium1 - LORPremium1 * totaltechnicalcommission / 100;
                var TargetLORPremium = TargetLORNetPremium / (1 - totalpolicycommission / 100);
                appln["lux_lossofrentpolicypremium"] = new Money(TargetLORPremium);

                TotalBIPremium = TotalBIPremium + TotalBIPremium * BISectionDiscount / 100;
                if (TotalBIPremium <= 75)
                {
                    TotalBIPremium = 75;
                }
                if (productName == "Commercial Combined" && appln.GetAttributeValue<bool>("lux_isbusinessinterruptioncoverrequired") == false)
                {
                    TotalBIPremium = 0;
                }

                TotalELPremium = TotalELPremium + TotalELPremium * ELSectionDiscount / 100;
                if (TotalELPremium <= 100 && appln.GetAttributeValue<bool>("lux_iselcoverrequired") == true)
                {
                    TotalELPremium = 100;
                }

                TotalPLPremium = TotalPLPremium + TotalPLPremium * PLSectionDiscount / 100;
                if (productName == "Retail")
                {
                    if (TotalPLPremium < 100)
                    {
                        TotalPLPremium = 100;
                    }
                }
                else if (productName == "Commercial Combined" && appln.GetAttributeValue<bool>("lux_ispublicandproductsliabilitycoverrequired") == true)
                {
                    if (TotalPLPremium < 100)
                    {
                        TotalPLPremium = 100;
                    }
                }
                else if (productName == "Pubs & Restaurants" || productName == "Hotels and Guesthouses")
                {
                    if (TotalPLPremium < 350)
                    {
                        TotalPLPremium = 350;
                    }
                }
                else if (productName == "Contractors Combined" && appln.GetAttributeValue<bool>("lux_ispublicandproductsliabilitycoverrequired") == true)
                {
                    if (TotalPLPremium < 250)
                    {
                        TotalPLPremium = 250;
                    }
                }

                TotalMoneyPremium = TotalMoneyPremium + TotalMoneyPremium * MDSectionDiscount / 100;
                if (TotalMoneyPremium <= 25)
                {
                    TotalMoneyPremium = 25;
                }
                if (productName == "Commercial Combined" && appln.GetAttributeValue<bool>("lux_ismoneycoverrequired") == false)
                {
                    TotalMoneyPremium = 0;
                }

                TotalAllRiskPremium = TotalAllRiskPremium + TotalAllRiskPremium * MDSectionDiscount / 100;

                TotalGITPremium = TotalGITPremium + TotalGITPremium * MDSectionDiscount / 100;
                if (TotalGITPremium <= 25 && appln.GetAttributeValue<bool>("lux_goodsintransit") == true)
                {
                    TotalGITPremium = 25;
                }

                if (productName == "Commercial Combined" || productName == "Retail")
                {
                    if (TotalPLPremium <= 100)
                    {
                        TotalPLPremium = 100;
                    }
                }
                else if (productName == "Pubs & Restaurants" || productName == "Hotels and Guesthouses")
                {
                    if (TotalPLPremium <= 350)
                    {
                        TotalPLPremium = 350;
                    }
                }
                else if (productName == "Contractors Combined")
                {
                    if (appln.GetAttributeValue<bool>("lux_ispublicandproductsliabilitycoverrequired") == false)
                    {
                        TotalPLPremium = 0;
                    }
                    else if (appln.GetAttributeValue<bool>("lux_ispublicandproductsliabilitycoverrequired") == true && TotalPLPremium <= 250)
                    {
                        TotalPLPremium = 250;
                    }

                    if (appln.GetAttributeValue<bool>("lux_iselcoverrequired") == true && TotalELPremium <= 250)
                    {
                        TotalELPremium = 250;
                    }

                    TotalCARPremium = TotalCARPremium + TotalCARPremium * CWSectionDiscount / 100;

                    if (appln.GetAttributeValue<bool>("lux_doyourequirecoverforcontractwork") == false)
                    {
                        TotalCARPremium = 0;
                    }
                    else if (appln.GetAttributeValue<bool>("lux_doyourequirecoverforcontractwork") == true && TotalCARPremium <= 350)
                    {
                        TotalCARPremium = 350;
                    }


                    //var CARPremium = appln.Attributes.Contains("lux_contractorspremium") ? appln.GetAttributeValue<Money>("lux_contractorspremium").Value : 0;
                    //CARPremium = CARPremium + CARPremium * CWSectionDiscount / 100;

                    //if (appln.GetAttributeValue<bool>("lux_doyourequirecoverforcontractwork") == false)
                    //{
                    //    CARPremium = 0;
                    //}
                    //else if (appln.GetAttributeValue<bool>("lux_doyourequirecoverforcontractwork") == true && CARPremium <= 350)
                    //{
                    //    CARPremium = 350;
                    //}

                    if (appln.GetAttributeValue<bool>("lux_ismaterialdamagecoverrequired") == false)
                    {
                        TotalMDPremium = 0;
                        TotalMoneyPremium = 0;
                        TotalBIPremium = 0;
                        TotalGITPremium = 0;
                        TotalAllRiskPremium = 0;
                    }
                    else
                    {
                        if (TotalMDPremium == 0)
                        {
                            TotalMDPremium = 0;
                        }
                        if (appln.GetAttributeValue<bool>("lux_ismoneycoverrequired") == false)
                        {
                            TotalMoneyPremium = 0;
                        }
                        if (appln.GetAttributeValue<bool>("lux_isbusinessinterruptioncoverrequired") == false)
                        {
                            TotalBIPremium = 0;
                        }
                        if (appln.GetAttributeValue<bool>("lux_goodsintransit") == false)
                        {
                            TotalGITPremium = 0;
                        }
                        if (appln.GetAttributeValue<bool>("lux_allriskitems") == false)
                        {
                            TotalAllRiskPremium = 0;
                        }
                    }
                }

                if (productName == "Commercial Combined")
                {
                    if (appln.GetAttributeValue<bool>("lux_ispublicandproductsliabilitycoverrequired") == false)
                    {
                        TotalPLPremium = 0;
                    }
                }

                var MDNetPremium = TotalMDPremium - TotalMDPremium * totaltechnicalcommission / 100;
                var BINetPremium = TotalBIPremium - TotalBIPremium * totaltechnicalcommission / 100;
                var MoneyNetPremium = TotalMoneyPremium - TotalMoneyPremium * totaltechnicalcommission / 100;
                var ARNetPremium = TotalAllRiskPremium - TotalAllRiskPremium * totaltechnicalcommission / 100;
                var GITNetPremium = TotalGITPremium - TotalGITPremium * totaltechnicalcommission / 100;

                var MDPolicyPremium = MDNetPremium / (1 - totalpolicycommission / 100);
                var BIPolicyPremium = BINetPremium / (1 - totalpolicycommission / 100);
                var MoneyPolicyPremium = MoneyNetPremium / (1 - totalpolicycommission / 100);
                var ARPolicyPremium = ARNetPremium / (1 - totalpolicycommission / 100);
                var GITPolicyPremium = GITNetPremium / (1 - totalpolicycommission / 100);

                appln["lux_materialdamagepolicypremium"] = new Money(MDPolicyPremium);
                appln["lux_businessinterruptionpolicypremium"] = new Money(BIPolicyPremium);
                appln["lux_moneypolicypremium"] = new Money(MoneyPolicyPremium);
                appln["lux_allriskpolicypremium"] = new Money(ARPolicyPremium);
                appln["lux_goodsintransitpolicypremium"] = new Money(GITPolicyPremium);

                var ELNetPremium = TotalELPremium - TotalELPremium * totaltechnicalcommission / 100;
                var PLNetPremium = TotalPLPremium - TotalPLPremium * totaltechnicalcommission / 100;
                var CARNetPremium = TotalCARPremium - TotalCARPremium * totaltechnicalcommission / 100;

                var ELPolicyPremium = ELNetPremium / (1 - totalpolicycommission / 100);
                var PLPolicyPremium = PLNetPremium / (1 - totalpolicycommission / 100);
                var CARPolicyPremium = CARNetPremium / (1 - totalpolicycommission / 100);

                appln["lux_employersliabilitypolicypremium"] = new Money(ELPolicyPremium);
                appln["lux_publicproductsliabilitypolicypremium"] = new Money(PLPolicyPremium);
                appln["lux_contractorspolicypremium"] = new Money(CARPolicyPremium);

                if (inceptionDate >= new DateTime(2023, 11, 01))
                {
                    ELNetPremium = TotalELPremium - TotalELPremium * totalLiabilitytechnicalcommission / 100;
                    ELPolicyPremium = ELNetPremium / (1 - totalLiabilitypolicycommission / 100);
                    appln["lux_employersliabilitypolicypremium"] = new Money(ELPolicyPremium);

                    PLNetPremium = TotalPLPremium - TotalPLPremium * totalLiabilitytechnicalcommission / 100;
                    PLPolicyPremium = PLNetPremium / (1 - totalLiabilitypolicycommission / 100);
                    appln["lux_publicproductsliabilitypolicypremium"] = new Money(PLPolicyPremium);

                    CARNetPremium = TotalCARPremium - TotalCARPremium * totalLiabilitytechnicalcommission / 100;
                    CARPolicyPremium = CARNetPremium / (1 - totalLiabilitypolicycommission / 100);
                    appln["lux_contractorspolicypremium"] = new Money(CARPolicyPremium);
                }

                appln["statuscode"] = new OptionSetValue(972970003);

                var TotalPolicyNetPremium = MDNetPremium + BINetPremium + ELNetPremium + PLNetPremium + MoneyNetPremium + ARNetPremium + GITNetPremium + TotalLENetPremium + CARNetPremium;
                appln["lux_policynetpremium"] = new Money(TotalPolicyNetPremium);

                var TotalPropertyPolicyPremium = MDPolicyPremium + BIPolicyPremium + MoneyPolicyPremium + ARPolicyPremium + GITPolicyPremium;
                var TotalLiabilityPolicyPremium = ELPolicyPremium + PLPolicyPremium + CARPolicyPremium;
                var TotalPolicyPremium = TotalPropertyPolicyPremium + TotalLiabilityPolicyPremium;

                appln["lux_lepolicygrosspremium"] = new Money(TotalLEGrossPremium);
                appln["lux_lepolicynetpremium"] = new Money(TotalLENetPremium);

                appln["lux_policybrokercommission"] = Convert.ToDouble(PolicyBrokerComm) + "%";
                appln["lux_policyaciescommission"] = Convert.ToDouble(PolicyaciesComm) + "%";
                var TotalPolComm = Convert.ToDouble(PolicyBrokerComm) + Convert.ToDouble(PolicyaciesComm);
                appln["lux_policytotalcommission"] = TotalPolComm + "%";

                var LEPolicyBrokerComm = TotalLEGrossPremium * PolicyBrokerComm / 100;
                var LePolicyGrossComm = TotalLEGrossPremium - TotalLENetPremium - LEPolicyBrokerComm;

                appln["lux_quotedpremiumbrokercommissionamount"] = new Money(TotalPolicyPremium * PolicyBrokerComm / 100 + LEPolicyBrokerComm);
                appln["lux_lepolicygrosscommission"] = new Money(LePolicyGrossComm);
                appln["lux_quotedpremiumaciescommissionamount"] = new Money(TotalPolicyPremium * PolicyaciesComm / 100);

                if (inceptionDate >= new DateTime(2023, 11, 01))
                {
                    appln["lux_quotedpremiumbrokercommissionamount"] = new Money(TotalPolicyPremium * PolicyBrokerComm / 100 + LEPolicyBrokerComm);
                    appln["lux_quotedpremiumaciescommissionamount"] = new Money(TotalPropertyPolicyPremium * PolicyaciesComm / 100 + TotalLiabilityPolicyPremium * PolicyLiabilityaciesComm / 100);
                    appln["lux_quotedpremiumaciescommissionamountliabili"] = new Money(TotalLiabilityPolicyPremium * PolicyLiabilityaciesComm / 100);
                    appln["lux_lepolicygrosscommission"] = new Money(LePolicyGrossComm);
                    appln["lux_policyaciescommissionliability"] = Convert.ToDouble(PolicyLiabilityaciesComm) + "%";
                }

                decimal Fee = 0;
                decimal PolicyFee = 0;

                var FeeFetch = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                                      <entity name='lux_adminfeerule'>
                                        <attribute name='lux_to' />
                                        <attribute name='lux_from' />
                                        <attribute name='lux_fee' />
                                        <attribute name='lux_adminfeeruleid' />
                                        <order attribute='lux_to' descending='false' />
                                        <filter type='and'>
                                          <condition attribute='statecode' operator='eq' value='0' />
                                          <condition attribute='lux_from' operator='le' value='{TotalPremium + TotalLEGrossPremium}' />
                                          <filter type='or'>
                                            <condition attribute='lux_to' operator='ge' value='{TotalPremium + TotalLEGrossPremium}' />
                                            <condition attribute='lux_to' operator='null' />
                                          </filter>
                                        </filter>
                                      </entity>
                                    </fetch>";
                if (service.RetrieveMultiple(new FetchExpression(FeeFetch)).Entities.Count > 0)
                {
                    Fee = service.RetrieveMultiple(new FetchExpression(FeeFetch)).Entities[0].GetAttributeValue<Money>("lux_fee").Value;
                }
                var PolicyFeeFetch = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                                      <entity name='lux_adminfeerule'>
                                        <attribute name='lux_to' />
                                        <attribute name='lux_from' />
                                        <attribute name='lux_fee' />
                                        <attribute name='lux_adminfeeruleid' />
                                        <order attribute='lux_to' descending='false' />
                                        <filter type='and'>
                                          <condition attribute='statecode' operator='eq' value='0' />
                                          <condition attribute='lux_from' operator='le' value='{TotalPolicyPremium + TotalLEGrossPremium}' />
                                          <filter type='or'>
                                            <condition attribute='lux_to' operator='ge' value='{TotalPolicyPremium + TotalLEGrossPremium}' />
                                            <condition attribute='lux_to' operator='null' />
                                          </filter>
                                        </filter>
                                      </entity>
                                    </fetch>";
                if (service.RetrieveMultiple(new FetchExpression(PolicyFeeFetch)).Entities.Count > 0)
                {
                    PolicyFee = service.RetrieveMultiple(new FetchExpression(PolicyFeeFetch)).Entities[0].GetAttributeValue<Money>("lux_fee").Value;
                }
                appln["lux_fees"] = new Money(Fee);
                appln["lux_policyfee"] = new Money(PolicyFee);
                appln["lux_quotationdate"] = DateTime.UtcNow;
                if (productName == "Retail")
                {
                    appln["lux_quotetype"] = true;
                }
                service.Update(appln);
                return "Success";
            }
            catch (Exception ex)
            {
                return "Failure";
            }
        }

        public static string CalculateTerrorismPremium(Entity appln, IOrganizationService service, bool IsLive)
        {
            var inceptionDate = Convert.ToDateTime(appln.FormattedValues["lux_inceptiondate"], System.Globalization.CultureInfo.GetCultureInfo("en-GB").DateTimeFormat);

            var terrfetch = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                                      <entity name='lux_terrorismpremise'>
                                        <attribute name='lux_riskpostcode' />
                                        <attribute name='lux_riskaddress' />
                                        <attribute name='lux_locationnumber' />
                                        <attribute name='lux_declaredvalueforrebuildingthisproperty' />
                                        <attribute name='lux_totalsuminsuredforthislocation' />
                                        <attribute name='lux_basisofcover' />
                                        <attribute name='lux_suminsuredwithupliftedamount' />
                                        <attribute name='lux_landlordscontentsinresidentialareas' />
                                        <attribute name='lux_lossofannualrentalincome' />
                                        <attribute name='lux_indemnityperiodrequired' />
                                        <attribute name='lux_terrorismpremiseid' />
                                        <order attribute='lux_riskpostcode' descending='false' />
                                        <filter type='and'>
                                          <condition attribute='statecode' operator='eq' value='0' />
                                          <condition attribute='lux_propertyownersapplication' operator='eq' uiname='' uitype='lux_propertyownersapplications' value='{appln.Id}' />
                                        </filter>
                                      </entity>
                                    </fetch>";

            if (service.RetrieveMultiple(new FetchExpression(terrfetch)).Entities.Count > 0)
            {
                decimal TerrorismTotal = 0;
                foreach (var item in service.RetrieveMultiple(new FetchExpression(terrfetch)).Entities)
                {
                    var premise_data = item;

                    var postcode = premise_data.Contains("lux_riskpostcode") ? premise_data.Attributes["lux_riskpostcode"] : "";
                    var post2digits = postcode.ToString().Substring(0, 2);
                    var post3digits = postcode.ToString().Substring(0, 3);
                    var post4digits = postcode.ToString().Substring(0, 4);
                    var zone = 972970003;
                    if (postcode.ToString() != "")
                    {
                        var TerrorismFetch = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                                                          <entity name='lux_terrorismratingzone'>
                                                            <attribute name='lux_name' />
                                                            <attribute name='lux_locationzone' />
                                                            <attribute name='lux_terrorismratingzoneid' />
                                                            <order attribute='lux_locationzone' descending='false' />
                                                            <order attribute='lux_name' descending='false' />
                                                            <filter type='and'>
                                                              <condition attribute='statecode' operator='eq' value='0' />
                                                              <condition attribute='lux_name' operator='eq' value='{post4digits}' />
                                                            </filter>
                                                          </entity>
                                                        </fetch>";
                        if (service.RetrieveMultiple(new FetchExpression(TerrorismFetch)).Entities.Count > 0)
                        {
                            zone = service.RetrieveMultiple(new FetchExpression(TerrorismFetch)).Entities[0].GetAttributeValue<OptionSetValue>("lux_locationzone").Value;
                        }
                        else
                        {
                            var TerrorismFetch1 = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                                                          <entity name='lux_terrorismratingzone'>
                                                            <attribute name='lux_name' />
                                                            <attribute name='lux_locationzone' />
                                                            <attribute name='lux_terrorismratingzoneid' />
                                                            <order attribute='lux_locationzone' descending='false' />
                                                            <order attribute='lux_name' descending='false' />
                                                            <filter type='and'>
                                                              <condition attribute='statecode' operator='eq' value='0' />
                                                              <condition attribute='lux_name' operator='eq' value='{post3digits}' />
                                                            </filter>
                                                          </entity>
                                                        </fetch>";
                            if (service.RetrieveMultiple(new FetchExpression(TerrorismFetch1)).Entities.Count > 0)
                            {
                                zone = service.RetrieveMultiple(new FetchExpression(TerrorismFetch1)).Entities[0].GetAttributeValue<OptionSetValue>("lux_locationzone").Value;
                            }
                            else
                            {
                                var TerrorismFetch2 = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                                                          <entity name='lux_terrorismratingzone'>
                                                            <attribute name='lux_name' />
                                                            <attribute name='lux_locationzone' />
                                                            <attribute name='lux_terrorismratingzoneid' />
                                                            <order attribute='lux_locationzone' descending='false' />
                                                            <order attribute='lux_name' descending='false' />
                                                            <filter type='and'>
                                                              <condition attribute='statecode' operator='eq' value='0' />
                                                              <condition attribute='lux_name' operator='eq' value='{post2digits}' />
                                                            </filter>
                                                          </entity>
                                                        </fetch>";
                                if (service.RetrieveMultiple(new FetchExpression(TerrorismFetch2)).Entities.Count > 0)
                                {
                                    zone = service.RetrieveMultiple(new FetchExpression(TerrorismFetch2)).Entities[0].GetAttributeValue<OptionSetValue>("lux_locationzone").Value;
                                }
                            }
                        }
                    }

                    var sum_Insured = premise_data.Contains("lux_totalsuminsuredforthislocation") ? premise_data.GetAttributeValue<Money>("lux_totalsuminsuredforthislocation").Value : 0;
                    var BISum_insured = premise_data.GetAttributeValue<Money>("lux_lossofannualrentalincome").Value;
                    var MDSum_insured = sum_Insured - BISum_insured;

                    decimal TerrorismPremium = 0;
                    decimal TerrorismMDPremium = 0;
                    decimal TerrorismBIPremium = 0;
                    decimal MDSI_rate = 0;
                    decimal BISI_rate = 0;

                    if (MDSum_insured > 0)
                    {
                        var MDRatesFetch = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                                                  <entity name='lux_terrorismrate'>
                                                    <attribute name='lux_ratebeforeanydiscount' />
                                                    <attribute name='lux_locationzone' />
                                                    <attribute name='lux_ratetype' />
                                                    <attribute name='lux_terrorismrateid' />
                                                    <order attribute='lux_ratetype' descending='false' />
                                                    <order attribute='lux_locationzone' descending='false' />
                                                    <filter type='and'>
                                                      <condition attribute='statecode' operator='eq' value='0' />
                                                      <condition attribute='lux_locationzone' operator='eq' value='{zone}' />
                                                      <condition attribute='lux_ratetype' operator='eq' value='972970002' />
                                                    </filter>
                                                  </entity>
                                                </fetch>";

                        if (service.RetrieveMultiple(new FetchExpression(MDRatesFetch)).Entities.Count > 0)
                        {
                            var SI_data = service.RetrieveMultiple(new FetchExpression(MDRatesFetch)).Entities[0];
                            if (SI_data.Contains("lux_ratebeforeanydiscount"))
                            {
                                MDSI_rate = SI_data.GetAttributeValue<decimal>("lux_ratebeforeanydiscount");
                            }
                            TerrorismMDPremium = MDSum_insured * MDSI_rate / 100;
                        }
                    }
                    if (BISum_insured > 0)
                    {
                        var BIRatesFetch = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                                                  <entity name='lux_terrorismrate'>
                                                    <attribute name='lux_ratebeforeanydiscount' />
                                                    <attribute name='lux_locationzone' />
                                                    <attribute name='lux_ratetype' />
                                                    <attribute name='lux_terrorismrateid' />
                                                    <order attribute='lux_ratetype' descending='false' />
                                                    <order attribute='lux_locationzone' descending='false' />
                                                    <filter type='and'>
                                                      <condition attribute='statecode' operator='eq' value='0' />
                                                      <condition attribute='lux_locationzone' operator='eq' value='{zone}' />
                                                      <condition attribute='lux_ratetype' operator='eq' value='972970001' />
                                                    </filter>
                                                  </entity>
                                                </fetch>";

                        if (service.RetrieveMultiple(new FetchExpression(BIRatesFetch)).Entities.Count > 0)
                        {
                            var SI_data = service.RetrieveMultiple(new FetchExpression(BIRatesFetch)).Entities[0];
                            if (SI_data.Contains("lux_ratebeforeanydiscount"))
                            {
                                BISI_rate = SI_data.GetAttributeValue<decimal>("lux_ratebeforeanydiscount");
                            }
                            TerrorismBIPremium = BISum_insured * BISI_rate / 100;
                        }
                    }
                    TerrorismPremium = TerrorismMDPremium + TerrorismBIPremium;

                    var item1 = service.Retrieve("lux_terrorismpremise", item.Id, new ColumnSet(true));
                    item1["lux_terrorismbipremium"] = new Money(TerrorismBIPremium);
                    item1["lux_terrorismmdpremium"] = new Money(TerrorismMDPremium);
                    item1["lux_terrorismbirate"] = BISI_rate;
                    item1["lux_terrorismmdrate"] = MDSI_rate;
                    item1["lux_terrorismpremium"] = new Money(TerrorismPremium);
                    item1["lux_terrorismzone"] = new OptionSetValue(zone);
                    service.Update(item1);

                    TerrorismTotal += TerrorismPremium;
                }

                if (inceptionDate >= new DateTime(2025, 05, 01))
                {
                    appln["lux_terrorismpremium"] = new Money(TerrorismTotal);
                    appln["lux_terrorismnetpremium"] = new Money(TerrorismTotal - TerrorismTotal * Convert.ToDecimal(37.5) / 100);
                    appln["lux_terrorismquotedpremium"] = new Money(TerrorismTotal);
                    appln["lux_terrorismpolicynetpremiumexcludingipt"] = new Money(TerrorismTotal - TerrorismTotal * Convert.ToDecimal(37.5) / 100);

                    appln["lux_terrorismbrokercommission"] = "22.5%";
                    appln["lux_terrorismbrokercommissionamount"] = new Money(TerrorismTotal * 22.5M / 100);
                    appln["lux_terrorismpolicybrokercommission"] = "22.5%";
                    appln["lux_terrorismquotedpremiumbrokercommissionamo"] = new Money(TerrorismTotal * 22.5M / 100);
                    appln["lux_terrorismaciescommission"] = "15.0%";
                    appln["lux_terrorismaciescommissionamout"] = new Money(TerrorismTotal * 15M / 100);
                    appln["lux_terrorismpolicyaciescommission"] = "15.0%";
                    appln["lux_terrorismquotedpremiumaciescommissionamou"] = new Money(TerrorismTotal * 15M / 100);
                    appln["lux_terrorismtotalcommission"] = "37.5%";
                    appln["lux_terrorismpolicytotalcommission"] = "37.5%";
                    appln["lux_quotationdate"] = DateTime.UtcNow;
                }
                else
                {
                    appln["lux_terrorismpremium"] = new Money(TerrorismTotal);
                    appln["lux_terrorismnetpremium"] = new Money(TerrorismTotal - TerrorismTotal * Convert.ToDecimal(32.5) / 100);
                    appln["lux_terrorismquotedpremium"] = new Money(TerrorismTotal);
                    appln["lux_terrorismpolicynetpremiumexcludingipt"] = new Money(TerrorismTotal - TerrorismTotal * Convert.ToDecimal(32.5) / 100);

                    appln["lux_terrorismbrokercommission"] = "20%";
                    appln["lux_terrorismbrokercommissionamount"] = new Money(TerrorismTotal * 20 / 100);
                    appln["lux_terrorismpolicybrokercommission"] = "20%";
                    appln["lux_terrorismquotedpremiumbrokercommissionamo"] = new Money(TerrorismTotal * 20 / 100);
                    appln["lux_terrorismaciescommission"] = "12.5%";
                    appln["lux_terrorismaciescommissionamout"] = new Money(TerrorismTotal * 12.5M / 100);
                    appln["lux_terrorismpolicyaciescommission"] = "12.5%";
                    appln["lux_terrorismquotedpremiumaciescommissionamou"] = new Money(TerrorismTotal * 12.5M / 100);
                    appln["lux_terrorismtotalcommission"] = "32.5%";
                    appln["lux_terrorismpolicytotalcommission"] = "32.5%";
                    appln["lux_quotationdate"] = DateTime.UtcNow;
                }

                service.Update(appln);
            }
            return "Success";
        }
    }
}
