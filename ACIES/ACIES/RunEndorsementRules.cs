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
    public class RunEndorsementRules : CodeActivity
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

            EntityReference applnref = Application.Get<EntityReference>(executionContext);
            Entity appln = new Entity(applnref.LogicalName, applnref.Id);
            appln = service.Retrieve("lux_propertyownersapplications", applnref.Id, new ColumnSet(true));

            var InceptionDate = Convert.ToDateTime(appln.FormattedValues["lux_inceptiondate"], System.Globalization.CultureInfo.GetCultureInfo("en-GB").DateTimeFormat);
            var RenewalDate = appln.GetAttributeValue<DateTime>("lux_renewaldate");
            var productName = Product.Get<string>(executionContext).ToString();
            var ProdusHazardGrade = appln.Attributes.Contains("lux_plproductshazardgroup") ? appln.GetAttributeValue<int>("lux_plproductshazardgroup") : 0;

            if (productName == "Property Owners" || productName == "Unoccupied")
            {
                var fetch = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                              <entity name='lux_propertyownerspremise'>
                                <attribute name='lux_riskpostcode' />
                                <attribute name='lux_riskaddress' />
                                <attribute name='lux_locationnumber' />
                                <attribute name='lux_tenanttype' />
                                <attribute name='lux_doesthepremiseshaveaflatroof' />
                                <attribute name='lux_flatroofpercentage' />
                                <attribute name='lux_doesthepremiseshaveabasement' />
                                <attribute name='lux_issubsidencecoverrequired' />
                                <attribute name='lux_occupancytype' />
                                <attribute name='lux_floodscore' />
                                <attribute name='lux_subsidencescore' />
                                <attribute name='lux_crimescore' />
                                <attribute name='lux_securityrating' />                                
                                <attribute name='lux_isthepremisesahouseofmultipleoccupation' />
                                <attribute name='lux_howmanybedroomsatthepremises' />
                                <attribute name='lux_dotheyhaveanhmolicence' />
                                <attribute name='lux_isthereanycookingfacilitiesintheindividua' />
                                <attribute name='lux_propertyownerspremiseid' />
                                <attribute name='lux_propertyownersapplication' />
                                <order attribute='lux_riskpostcode' descending='false' />
                                <filter type='and'>
                                  <condition attribute='statecode' operator='eq' value='0' />
                                  <condition attribute='lux_propertyownersapplication' operator='eq' uiname='' uitype='lux_propertyownersapplications' value='{appln.Id}' />
                                </filter>
                                <link-entity name='lux_propertyownersapplications' from='lux_propertyownersapplicationsid' to='lux_propertyownersapplication' visible='false' link-type='outer' alias='poa'>
                                  <attribute name='lux_rpocpoproducttype' />
                                  <attribute name='lux_broker' />
                                </link-entity>
                              </entity>
                            </fetch>";

                if (service.RetrieveMultiple(new FetchExpression(fetch)).Entities.Count > 0)
                {
                    var endorsementFetch = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                                                          <entity name='lux_endorsementlibrary'>
                                                            <attribute name='lux_name' />
                                                            <attribute name='new_product' />
                                                            <attribute name='lux_insurer' />
                                                            <attribute name='lux_endorsementdescription' />
                                                            <attribute name='new_endorsementnumber' />
                                                            <attribute name='lux_endorsementhtml' />
                                                            <attribute name='lux_endorsementlibraryid' />
                                                            <order attribute='lux_name' descending='false' />
                                                            <filter type='and'>
                                                              <condition attribute='statecode' operator='eq' value='0' />
                                                              <condition attribute='lux_name' operator='not-null' />
                                                              <filter type='or'>
                                                                <condition attribute='new_product' operator='eq' uiname='Property Owners' uitype='product' value='{"5CAE3BD2-1F78-EB11-A812-00224841494B"}' />
                                                                <condition attribute='new_product' operator='null' />
                                                              </filter>
                                                            </filter>
                                                          </entity>
                                                        </fetch>";

                    foreach (var item in service.RetrieveMultiple(new FetchExpression(fetch)).Entities)
                    {
                        var TenentType = item.Attributes.Contains("lux_tenanttype") ? item.GetAttributeValue<OptionSetValue>("lux_tenanttype").Value : 0;
                        var FlatRoof = item.Attributes.Contains("lux_doesthepremiseshaveaflatroof") ? item.GetAttributeValue<bool>("lux_doesthepremiseshaveaflatroof") : false;
                        var OccupancyType = item.Attributes.Contains("lux_occupancytype") ? item.GetAttributeValue<OptionSetValue>("lux_occupancytype").Value : 0;
                        var POProuctType = item.Attributes.Contains("poa.lux_rpocpoproducttype") ? ((OptionSetValue)((item.GetAttributeValue<AliasedValue>("poa.lux_rpocpoproducttype")).Value)).Value : 0;
                        var Basement = item.Attributes.Contains("lux_doesthepremiseshaveabasement") ? item.GetAttributeValue<bool>("lux_doesthepremiseshaveabasement") : false;
                        var IsHMO = item.Attributes.Contains("lux_isthepremisesahouseofmultipleoccupation") ? item.GetAttributeValue<bool>("lux_isthepremisesahouseofmultipleoccupation") : false;
                        var NoofBedrooms = item.Attributes.Contains("lux_howmanybedroomsatthepremises") ? item.GetAttributeValue<int>("lux_howmanybedroomsatthepremises") : 0;
                        var HMOLicense = item.Attributes.Contains("lux_dotheyhaveanhmolicence") ? item.GetAttributeValue<bool>("lux_dotheyhaveanhmolicence") : true;
                        var AnyCoockingFacility = item.Attributes.Contains("lux_isthereanycookingfacilitiesintheindividua") ? item.GetAttributeValue<bool>("lux_isthereanycookingfacilitiesintheindividua") : false;

                        if (service.RetrieveMultiple(new FetchExpression(endorsementFetch)).Entities.Count > 0)
                        {
                            //if (model.IsLive == 1)
                            //{
                            //    var Endorsement1 = service.RetrieveMultiple(new FetchExpression(endorsementFetch)).Entities.FirstOrDefault(x => x.Attributes["lux_name"].ToString() == "Increased Excess Malicious Damage - Students");
                            //    if (Endorsement1 != null)
                            //    {
                            //        var fetch1 = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                            //                          <entity name='lux_applicationendorsements'>
                            //                            <attribute name='lux_applicationendorsementsid' />
                            //                            <attribute name='lux_name' />
                            //                            <attribute name='lux_endorsementnumber' />
                            //                            <attribute name='createdon' />
                            //                            <order attribute='lux_name' descending='false' />
                            //                            <filter type='and'>
                            //                              <condition attribute='statecode' operator='eq' value='0' />
                            //                              <condition attribute='lux_endorsementlibrary' operator='eq' uiname='' uitype='lux_endorsementlibrary' value='{Endorsement1.Attributes["lux_endorsementlibraryid"]}' />
                            //                              <condition attribute='lux_application' operator='eq' uiname='' uitype='lux_propertyownersapplications' value='{appln.Id}' />
                            //                            </filter>
                            //                          </entity>
                            //                        </fetch>";

                            //        if (((OccupancyType == 972970002 || OccupancyType == 972970004)) && TenentType == 972970003) // Residential and Student
                            //        {
                            //            if (service.RetrieveMultiple(new FetchExpression(fetch1)).Entities.Count == 0)
                            //            {
                            //                Entity ent = new Entity("lux_applicationendorsements");
                            //                ent["lux_isdefault"] = true;
                            //                ent["lux_endorsementnumber"] = Endorsement1.Attributes["new_endorsementnumber"];
                            //                ent["lux_endorsementhtml"] = Endorsement1.Attributes["lux_endorsementhtml"];
                            //                ent["lux_name"] = Endorsement1.Attributes["lux_name"];
                            //                ent["lux_endorsementlibrary"] = new EntityReference("lux_endorsementlibrary", new Guid(Endorsement1.Attributes["lux_endorsementlibraryid"].ToString()));
                            //                ent["lux_application"] = new EntityReference("lux_propertyownersapplications", appln.Id);
                            //                service.Create(ent);
                            //            }
                            //        }
                            //        //else
                            //        //{
                            //        //    if (service.RetrieveMultiple(new FetchExpression(fetch1)).Entities.Count > 0)
                            //        //    {
                            //        //        service.Delete("lux_applicationendorsements", service.RetrieveMultiple(new FetchExpression(fetch1)).Entities[0].Id);
                            //        //    }
                            //        //}
                            //    }
                            //}

                            var Endorsement2 = service.RetrieveMultiple(new FetchExpression(endorsementFetch)).Entities.FirstOrDefault(x => x.Attributes["lux_name"].ToString() == "Restricted Perils");
                            if (Endorsement2 != null)
                            {
                                var fetch1 = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                                                          <entity name='lux_applicationendorsements'>
                                                            <attribute name='lux_applicationendorsementsid' />
                                                            <attribute name='lux_name' />
                                                            <attribute name='lux_endorsementnumber' />
                                                            <attribute name='createdon' />
                                                            <order attribute='lux_name' descending='false' />
                                                            <filter type='and'>
                                                              <condition attribute='statecode' operator='eq' value='0' />
                                                              <condition attribute='lux_endorsementlibrary' operator='eq' uiname='' uitype='lux_endorsementlibrary' value='{Endorsement2.Attributes["lux_endorsementlibraryid"]}' />
                                                              <condition attribute='lux_application' operator='eq' uiname='' uitype='lux_propertyownersapplications' value='{appln.Id}' />
                                                            </filter>
                                                          </entity>
                                                        </fetch>";

                                if (OccupancyType == 972970003 || (POProuctType == 972970003 || POProuctType == 972970004)) // Unoccupied
                                {
                                    if (service.RetrieveMultiple(new FetchExpression(fetch1)).Entities.Count == 0)
                                    {
                                        Entity ent = new Entity("lux_applicationendorsements");
                                        ent["lux_isdefault"] = true;
                                        ent["lux_endorsementhtml"] = Endorsement2.Attributes["lux_endorsementhtml"];
                                        ent["lux_endorsementnumber"] = Endorsement2.Attributes["new_endorsementnumber"];
                                        ent["lux_name"] = Endorsement2.Attributes["lux_name"];
                                        ent["lux_endorsementlibrary"] = new EntityReference("lux_endorsementlibrary", new Guid(Endorsement2.Attributes["lux_endorsementlibraryid"].ToString()));
                                        ent["lux_application"] = new EntityReference("lux_propertyownersapplications", appln.Id);
                                        service.Create(ent);
                                    }
                                }
                                //else
                                //{
                                //    if (service.RetrieveMultiple(new FetchExpression(fetch1)).Entities.Count > 0)
                                //    {
                                //        service.Delete("lux_applicationendorsements", service.RetrieveMultiple(new FetchExpression(fetch1)).Entities[0].Id);
                                //    }
                                //}
                            }

                            var Endorsement3 = service.RetrieveMultiple(new FetchExpression(endorsementFetch)).Entities.FirstOrDefault(x => x.Attributes["lux_name"].ToString() == "Roof Maintenance Condition");
                            if (Endorsement3 != null)
                            {
                                var fetch1 = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                                                          <entity name='lux_applicationendorsements'>
                                                            <attribute name='lux_applicationendorsementsid' />
                                                            <attribute name='lux_name' />
                                                            <attribute name='lux_endorsementnumber' />
                                                            <attribute name='createdon' />
                                                            <order attribute='lux_name' descending='false' />
                                                            <filter type='and'>
                                                              <condition attribute='statecode' operator='eq' value='0' />
                                                              <condition attribute='lux_endorsementlibrary' operator='eq' uiname='' uitype='lux_endorsementlibrary' value='{Endorsement3.Attributes["lux_endorsementlibraryid"]}' />
                                                              <condition attribute='lux_application' operator='eq' uiname='' uitype='lux_propertyownersapplications' value='{appln.Id}' />
                                                            </filter>
                                                          </entity>
                                                        </fetch>";

                                if (FlatRoof == true) // Flat roof
                                {
                                    if (service.RetrieveMultiple(new FetchExpression(fetch1)).Entities.Count == 0)
                                    {
                                        Entity ent = new Entity("lux_applicationendorsements");
                                        ent["lux_isdefault"] = true;
                                        ent["lux_endorsementhtml"] = Endorsement3.Attributes["lux_endorsementhtml"];
                                        ent["lux_endorsementnumber"] = Endorsement3.Attributes["new_endorsementnumber"];
                                        ent["lux_name"] = Endorsement3.Attributes["lux_name"];
                                        ent["lux_endorsementlibrary"] = new EntityReference("lux_endorsementlibrary", new Guid(Endorsement3.Attributes["lux_endorsementlibraryid"].ToString()));
                                        ent["lux_application"] = new EntityReference("lux_propertyownersapplications", appln.Id);
                                        service.Create(ent);
                                    }
                                }
                                //else
                                //{
                                //    if (service.RetrieveMultiple(new FetchExpression(fetch1)).Entities.Count > 0)
                                //    {
                                //        service.Delete("lux_applicationendorsements", service.RetrieveMultiple(new FetchExpression(fetch1)).Entities[0].Id);
                                //    }
                                //}
                            }

                            var FloodScore = item.Attributes.Contains("lux_floodscore") ? item.GetAttributeValue<int>("lux_floodscore") : 0;
                            var SubsidienceScore = item.Attributes.Contains("lux_subsidencescore") ? item.GetAttributeValue<int>("lux_subsidencescore") : 0;

                            var Endorsement4 = service.RetrieveMultiple(new FetchExpression(endorsementFetch)).Entities.FirstOrDefault(x => x.Attributes["lux_name"].ToString() == "Increased Flood Excess - £500");
                            if (Endorsement4 != null)
                            {
                                var TexttoAppend = "";
                                var premiseFetch = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                                                          <entity name='lux_propertyownerspremise'>
                                                            <attribute name='lux_riskpostcode' />
                                                            <attribute name='lux_locationnumber' />
                                                            <attribute name='lux_floodscore' />
                                                            <attribute name='lux_crimescore' />
                                                            <attribute name='lux_propertyownerspremiseid' />
                                                            <attribute name='lux_riskaddress' />
                                                            <attribute name='lux_housenumber' />
                                                            <attribute name='lux_citycounty' />
                                                            <order attribute='lux_locationnumber' descending='false' />
                                                            <filter type='and'>
                                                              <condition attribute='lux_floodscore' operator='eq' value='9' />
                                                              <condition attribute='lux_propertyownersapplication' operator='eq' uiname='' uitype='lux_propertyownersapplications' value='{appln.Id}' />
                                                            </filter>
                                                          </entity>
                                                        </fetch>";

                                if (service.RetrieveMultiple(new FetchExpression(premiseFetch)).Entities.Count > 0)
                                {
                                    var count = service.RetrieveMultiple(new FetchExpression(premiseFetch)).Entities.Count;
                                    if (count == 1)
                                    {
                                        var premiseData = service.RetrieveMultiple(new FetchExpression(premiseFetch)).Entities[0];
                                        var premiseNumber = premiseData.Contains("lux_locationnumber") ? premiseData.Attributes["lux_locationnumber"] : "";
                                        var houseNumber = premiseData.Contains("lux_housenumber") ? premiseData.Attributes["lux_housenumber"] : "";
                                        var street = premiseData.Contains("lux_riskaddress") ? premiseData.Attributes["lux_riskaddress"] : "";
                                        var city = premiseData.Contains("lux_citycounty") ? premiseData.Attributes["lux_citycounty"] : "";
                                        var postcode = premiseData.Contains("lux_riskpostcode") ? premiseData.Attributes["lux_riskpostcode"] : "";

                                        TexttoAppend = "Premises " + premiseNumber + " - " + houseNumber + " " + street + " " + city + " " + postcode + " only.".Replace("  ", " ");
                                    }
                                    else if (count > 1)
                                    {
                                        foreach (var riskItem in service.RetrieveMultiple(new FetchExpression(premiseFetch)).Entities)
                                        {
                                            var premiseNumber = riskItem.Contains("lux_locationnumber") ? riskItem.Attributes["lux_locationnumber"] : "";
                                            var houseNumber = riskItem.Contains("lux_housenumber") ? riskItem.Attributes["lux_housenumber"] : "";
                                            var street = riskItem.Contains("lux_riskaddress") ? riskItem.Attributes["lux_riskaddress"] : "";
                                            var city = riskItem.Contains("lux_citycounty") ? riskItem.Attributes["lux_citycounty"] : "";
                                            var postcode = riskItem.Contains("lux_riskpostcode") ? riskItem.Attributes["lux_riskpostcode"] : "";
                                            TexttoAppend += "<br>Premises " + premiseNumber + " - " + houseNumber + " " + street + " " + city + " " + postcode + "".Replace("  ", " ");
                                        }
                                    }
                                }

                                var fetch1 = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                                                          <entity name='lux_applicationendorsements'>
                                                            <attribute name='lux_applicationendorsementsid' />
                                                            <attribute name='lux_name' />
                                                            <attribute name='lux_endorsementnumber' />
                                                            <attribute name='createdon' />
                                                            <order attribute='lux_name' descending='false' />
                                                            <filter type='and'>
                                                              <condition attribute='statecode' operator='eq' value='0' />
                                                              <condition attribute='lux_endorsementlibrary' operator='eq' uiname='' uitype='lux_endorsementlibrary' value='{Endorsement4.Attributes["lux_endorsementlibraryid"]}' />
                                                              <condition attribute='lux_application' operator='eq' uiname='' uitype='lux_propertyownersapplications' value='{appln.Id}' />
                                                            </filter>
                                                          </entity>
                                                        </fetch>";

                                if (FloodScore == 9) //£500 Flood Excess
                                {
                                    if (service.RetrieveMultiple(new FetchExpression(fetch1)).Entities.Count == 0)
                                    {
                                        Entity ent = new Entity("lux_applicationendorsements");
                                        ent["lux_isdefault"] = true;
                                        ent["lux_endorsementnumber"] = Endorsement4.Attributes["new_endorsementnumber"];
                                        ent["lux_endorsementhtml"] = Endorsement4.Attributes["lux_endorsementhtml"].ToString().Replace("XXXX", TexttoAppend);
                                        ent["lux_name"] = Endorsement4.Attributes["lux_name"];
                                        ent["lux_endorsementlibrary"] = new EntityReference("lux_endorsementlibrary", new Guid(Endorsement4.Attributes["lux_endorsementlibraryid"].ToString()));
                                        ent["lux_application"] = new EntityReference("lux_propertyownersapplications", appln.Id);
                                        service.Create(ent);
                                    }
                                }
                                //else
                                //{
                                //    if (service.RetrieveMultiple(new FetchExpression(fetch1)).Entities.Count > 0)
                                //    {
                                //        service.Delete("lux_applicationendorsements", service.RetrieveMultiple(new FetchExpression(fetch1)).Entities[0].Id);
                                //    }
                                //}
                            }

                            var Endorsement5 = service.RetrieveMultiple(new FetchExpression(endorsementFetch)).Entities.FirstOrDefault(x => x.Attributes["lux_name"].ToString() == "Increased Flood Excess - £1,000");
                            if (Endorsement5 != null)
                            {
                                var TexttoAppend = "";
                                var premiseFetch = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                                                          <entity name='lux_propertyownerspremise'>
                                                            <attribute name='lux_riskpostcode' />
                                                            <attribute name='lux_locationnumber' />
                                                            <attribute name='lux_floodscore' />
                                                            <attribute name='lux_crimescore' />
                                                            <attribute name='lux_propertyownerspremiseid' />
                                                            <attribute name='lux_riskaddress' />
                                                            <attribute name='lux_housenumber' />
                                                            <attribute name='lux_citycounty' />
                                                            <order attribute='lux_locationnumber' descending='false' />
                                                            <filter type='and'>
                                                              <condition attribute='lux_floodscore' operator='eq' value='10' />
                                                              <condition attribute='lux_propertyownersapplication' operator='eq' uiname='' uitype='lux_propertyownersapplications' value='{appln.Id}' />
                                                            </filter>
                                                          </entity>
                                                        </fetch>";

                                if (service.RetrieveMultiple(new FetchExpression(premiseFetch)).Entities.Count > 0)
                                {
                                    var count = service.RetrieveMultiple(new FetchExpression(premiseFetch)).Entities.Count;
                                    if (count == 1)
                                    {
                                        var premiseData = service.RetrieveMultiple(new FetchExpression(premiseFetch)).Entities[0];
                                        var premiseNumber = premiseData.Contains("lux_locationnumber") ? premiseData.Attributes["lux_locationnumber"] : "";
                                        var houseNumber = premiseData.Contains("lux_housenumber") ? premiseData.Attributes["lux_housenumber"] : "";
                                        var street = premiseData.Contains("lux_riskaddress") ? premiseData.Attributes["lux_riskaddress"] : "";
                                        var city = premiseData.Contains("lux_citycounty") ? premiseData.Attributes["lux_citycounty"] : "";
                                        var postcode = premiseData.Contains("lux_riskpostcode") ? premiseData.Attributes["lux_riskpostcode"] : "";

                                        TexttoAppend = "Premises " + premiseNumber + " - " + houseNumber + " " + street + " " + city + " " + postcode + " only.".Replace("  ", " ");
                                    }
                                    else if (count > 1)
                                    {
                                        foreach (var riskItem in service.RetrieveMultiple(new FetchExpression(premiseFetch)).Entities)
                                        {
                                            var premiseNumber = riskItem.Contains("lux_locationnumber") ? riskItem.Attributes["lux_locationnumber"] : "";
                                            var houseNumber = riskItem.Contains("lux_housenumber") ? riskItem.Attributes["lux_housenumber"] : "";
                                            var street = riskItem.Contains("lux_riskaddress") ? riskItem.Attributes["lux_riskaddress"] : "";
                                            var city = riskItem.Contains("lux_citycounty") ? riskItem.Attributes["lux_citycounty"] : "";
                                            var postcode = riskItem.Contains("lux_riskpostcode") ? riskItem.Attributes["lux_riskpostcode"] : "";
                                            TexttoAppend += "<br>Premises " + premiseNumber + " - " + houseNumber + " " + street + " " + city + " " + postcode + "".Replace("  ", " ");
                                        }
                                    }
                                }

                                var fetch1 = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                                                          <entity name='lux_applicationendorsements'>
                                                            <attribute name='lux_applicationendorsementsid' />
                                                            <attribute name='lux_name' />
                                                            <attribute name='lux_endorsementnumber' />
                                                            <attribute name='createdon' />
                                                            <order attribute='lux_name' descending='false' />
                                                            <filter type='and'>
                                                              <condition attribute='statecode' operator='eq' value='0' />
                                                              <condition attribute='lux_endorsementlibrary' operator='eq' uiname='' uitype='lux_endorsementlibrary' value='{Endorsement5.Attributes["lux_endorsementlibraryid"]}' />
                                                              <condition attribute='lux_application' operator='eq' uiname='' uitype='lux_propertyownersapplications' value='{appln.Id}' />
                                                            </filter>
                                                          </entity>
                                                        </fetch>";
                                //if (model.IsLive == 0)
                                //{
                                if (FloodScore == 10 || Basement == true) //£1000 Flood Excess
                                {
                                    if (service.RetrieveMultiple(new FetchExpression(fetch1)).Entities.Count == 0)
                                    {
                                        Entity ent = new Entity("lux_applicationendorsements");
                                        ent["lux_isdefault"] = true;
                                        ent["lux_endorsementnumber"] = Endorsement5.Attributes["new_endorsementnumber"];
                                        ent["lux_endorsementhtml"] = Endorsement5.Attributes["lux_endorsementhtml"].ToString().Replace("XXXX", TexttoAppend);
                                        ent["lux_name"] = Endorsement5.Attributes["lux_name"];
                                        ent["lux_endorsementlibrary"] = new EntityReference("lux_endorsementlibrary", new Guid(Endorsement5.Attributes["lux_endorsementlibraryid"].ToString()));
                                        ent["lux_application"] = new EntityReference("lux_propertyownersapplications", appln.Id);
                                        service.Create(ent);
                                    }
                                }
                                //else
                                //{
                                //    if (service.RetrieveMultiple(new FetchExpression(fetch1)).Entities.Count > 0)
                                //    {
                                //        service.Delete("lux_applicationendorsements", service.RetrieveMultiple(new FetchExpression(fetch1)).Entities[0].Id);
                                //    }
                                //}
                                //}
                                //else
                                //{
                                //    if (FloodScore == 10) //£1000 Flood Excess
                                //    {
                                //        if (service.RetrieveMultiple(new FetchExpression(fetch1)).Entities.Count == 0)
                                //        {
                                //            Entity ent = new Entity("lux_applicationendorsements");
                                //            ent["lux_isdefault"] = true;
                                //            ent["lux_endorsementnumber"] = Endorsement5.Attributes["new_endorsementnumber"];
                                //            ent["lux_endorsementhtml"] = Endorsement5.Attributes["lux_endorsementhtml"].ToString().Replace("XXXX", TexttoAppend);
                                //            ent["lux_name"] = Endorsement5.Attributes["lux_name"];
                                //            ent["lux_endorsementlibrary"] = new EntityReference("lux_endorsementlibrary", new Guid(Endorsement5.Attributes["lux_endorsementlibraryid"].ToString()));
                                //            ent["lux_application"] = new EntityReference("lux_propertyownersapplications", appln.Id);
                                //            service.Create(ent);
                                //        }
                                //    }
                                //    //else
                                //    //{
                                //    //    if (service.RetrieveMultiple(new FetchExpression(fetch1)).Entities.Count > 0)
                                //    //    {
                                //    //        service.Delete("lux_applicationendorsements", service.RetrieveMultiple(new FetchExpression(fetch1)).Entities[0].Id);
                                //    //    }
                                //    //}
                                //}
                            }

                            var Endorsement6 = service.RetrieveMultiple(new FetchExpression(endorsementFetch)).Entities.FirstOrDefault(x => x.Attributes["lux_name"].ToString() == "Flood Exclusion");
                            if (Endorsement6 != null)
                            {
                                var TexttoAppend = "";
                                var premiseFetch = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                                                          <entity name='lux_propertyownerspremise'>
                                                            <attribute name='lux_riskpostcode' />
                                                            <attribute name='lux_locationnumber' />
                                                            <attribute name='lux_floodscore' />
                                                            <attribute name='lux_crimescore' />
                                                            <attribute name='lux_propertyownerspremiseid' />
                                                            <attribute name='lux_riskaddress' />
                                                            <attribute name='lux_housenumber' />
                                                            <attribute name='lux_citycounty' />
                                                            <order attribute='lux_locationnumber' descending='false' />
                                                            <filter type='and'>
                                                              <condition attribute='lux_floodscore' operator='ge' value='11' />
                                                              <condition attribute='lux_propertyownersapplication' operator='eq' uiname='' uitype='lux_propertyownersapplications' value='{appln.Id}' />
                                                            </filter>
                                                          </entity>
                                                        </fetch>";

                                if (service.RetrieveMultiple(new FetchExpression(premiseFetch)).Entities.Count > 0)
                                {
                                    var count = service.RetrieveMultiple(new FetchExpression(premiseFetch)).Entities.Count;
                                    if (count == 1)
                                    {
                                        var premiseData = service.RetrieveMultiple(new FetchExpression(premiseFetch)).Entities[0];
                                        var premiseNumber = premiseData.Contains("lux_locationnumber") ? premiseData.Attributes["lux_locationnumber"] : "";
                                        var houseNumber = premiseData.Contains("lux_housenumber") ? premiseData.Attributes["lux_housenumber"] : "";
                                        var street = premiseData.Contains("lux_riskaddress") ? premiseData.Attributes["lux_riskaddress"] : "";
                                        var city = premiseData.Contains("lux_citycounty") ? premiseData.Attributes["lux_citycounty"] : "";
                                        var postcode = premiseData.Contains("lux_riskpostcode") ? premiseData.Attributes["lux_riskpostcode"] : "";

                                        TexttoAppend = "Premises " + premiseNumber + " - " + houseNumber + " " + street + " " + city + " " + postcode + " only.".Replace("  ", " ");
                                    }
                                    else if (count > 1)
                                    {
                                        foreach (var riskItem in service.RetrieveMultiple(new FetchExpression(premiseFetch)).Entities)
                                        {
                                            var premiseNumber = riskItem.Contains("lux_locationnumber") ? riskItem.Attributes["lux_locationnumber"] : "";
                                            var houseNumber = riskItem.Contains("lux_housenumber") ? riskItem.Attributes["lux_housenumber"] : "";
                                            var street = riskItem.Contains("lux_riskaddress") ? riskItem.Attributes["lux_riskaddress"] : "";
                                            var city = riskItem.Contains("lux_citycounty") ? riskItem.Attributes["lux_citycounty"] : "";
                                            var postcode = riskItem.Contains("lux_riskpostcode") ? riskItem.Attributes["lux_riskpostcode"] : "";
                                            TexttoAppend += "<br>Premises " + premiseNumber + " - " + houseNumber + " " + street + " " + city + " " + postcode + "".Replace("  ", " ");
                                        }
                                    }
                                }

                                var fetch1 = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                                                          <entity name='lux_applicationendorsements'>
                                                            <attribute name='lux_applicationendorsementsid' />
                                                            <attribute name='lux_name' />
                                                            <attribute name='lux_endorsementnumber' />
                                                            <attribute name='createdon' />
                                                            <order attribute='lux_name' descending='false' />
                                                            <filter type='and'>
                                                              <condition attribute='statecode' operator='eq' value='0' />
                                                              <condition attribute='lux_endorsementlibrary' operator='eq' uiname='' uitype='lux_endorsementlibrary' value='{Endorsement6.Attributes["lux_endorsementlibraryid"]}' />
                                                              <condition attribute='lux_application' operator='eq' uiname='' uitype='lux_propertyownersapplications' value='{appln.Id}' />
                                                            </filter>
                                                          </entity>
                                                        </fetch>";
                                if (FloodScore >= 11) //Decline - Exclude Flood Cover
                                {
                                    if (service.RetrieveMultiple(new FetchExpression(fetch1)).Entities.Count == 0)
                                    {
                                        Entity ent = new Entity("lux_applicationendorsements");
                                        ent["lux_isdefault"] = true;
                                        ent["lux_endorsementnumber"] = Endorsement6.Attributes["new_endorsementnumber"];
                                        ent["lux_endorsementhtml"] = Endorsement6.Attributes["lux_endorsementhtml"].ToString().Replace("XXXX", TexttoAppend);
                                        ent["lux_name"] = Endorsement6.Attributes["lux_name"];
                                        ent["lux_endorsementlibrary"] = new EntityReference("lux_endorsementlibrary", new Guid(Endorsement6.Attributes["lux_endorsementlibraryid"].ToString()));
                                        ent["lux_application"] = new EntityReference("lux_propertyownersapplications", appln.Id);
                                        service.Create(ent);
                                    }
                                }
                                //else
                                //{
                                //    if (service.RetrieveMultiple(new FetchExpression(fetch1)).Entities.Count > 0)
                                //    {
                                //        service.Delete("lux_applicationendorsements", service.RetrieveMultiple(new FetchExpression(fetch1)).Entities[0].Id);
                                //    }
                                //}
                            }
                            //if (model.IsLive == 1)
                            //{
                            //    var Endorsement7 = service.RetrieveMultiple(new FetchExpression(endorsementFetch)).Entities.FirstOrDefault(x => x.Attributes["lux_name"].ToString() == "Increased Subsidence Excess - £2,500");
                            //    if (Endorsement7 != null)
                            //    {
                            //        var TexttoAppend = "";
                            //        var premiseFetch = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                            //                          <entity name='lux_propertyownerspremise'>
                            //                            <attribute name='lux_riskpostcode' />
                            //                            <attribute name='lux_locationnumber' />
                            //                            <attribute name='lux_floodscore' />
                            //                            <attribute name='lux_subsidencescore' />
                            //                            <attribute name='lux_propertyownerspremiseid' />
                            //                            <attribute name='lux_riskaddress' />
                            //                            <attribute name='lux_housenumber' />
                            //                            <attribute name='lux_citycounty' />
                            //                            <order attribute='lux_locationnumber' descending='false' />
                            //                            <filter type='and'>
                            //                              <condition attribute='lux_subsidencescore' operator='eq' value='4' />
                            //                              <condition attribute='lux_propertyownersapplication' operator='eq' uiname='' uitype='lux_propertyownersapplications' value='{appln.Id}' />
                            //                            </filter>
                            //                          </entity>
                            //                        </fetch>";

                            //        if (service.RetrieveMultiple(new FetchExpression(premiseFetch)).Entities.Count > 0)
                            //        {
                            //            var count = service.RetrieveMultiple(new FetchExpression(premiseFetch)).Entities.Count;
                            //            if (count == 1)
                            //            {
                            //                var premiseData = service.RetrieveMultiple(new FetchExpression(premiseFetch)).Entities[0];
                            //                var premiseNumber = premiseData.Contains("lux_locationnumber") ? premiseData.Attributes["lux_locationnumber"] : "";
                            //                var houseNumber = premiseData.Contains("lux_housenumber") ? premiseData.Attributes["lux_housenumber"] : "";
                            //                var street = premiseData.Contains("lux_riskaddress") ? premiseData.Attributes["lux_riskaddress"] : "";
                            //                var city = premiseData.Contains("lux_citycounty") ? premiseData.Attributes["lux_citycounty"] : "";
                            //                var postcode = premiseData.Contains("lux_riskpostcode") ? premiseData.Attributes["lux_riskpostcode"] : "";

                            //                TexttoAppend = "Premises " + premiseNumber + " - " + houseNumber + " " + street + " " + city + " " + postcode + " only.".Replace("  ", " ");
                            //            }
                            //            else if (count > 1)
                            //            {
                            //                foreach (var riskItem in service.RetrieveMultiple(new FetchExpression(premiseFetch)).Entities)
                            //                {
                            //                    var premiseNumber = riskItem.Contains("lux_locationnumber") ? riskItem.Attributes["lux_locationnumber"] : "";
                            //                    var houseNumber = riskItem.Contains("lux_housenumber") ? riskItem.Attributes["lux_housenumber"] : "";
                            //                    var street = riskItem.Contains("lux_riskaddress") ? riskItem.Attributes["lux_riskaddress"] : "";
                            //                    var city = riskItem.Contains("lux_citycounty") ? riskItem.Attributes["lux_citycounty"] : "";
                            //                    var postcode = riskItem.Contains("lux_riskpostcode") ? riskItem.Attributes["lux_riskpostcode"] : "";
                            //                    TexttoAppend += "<br>Premises " + premiseNumber + " - " + houseNumber + " " + street + " " + city + " " + postcode + "".Replace("  ", " ");
                            //                }
                            //            }
                            //        }

                            //        var fetch1 = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                            //                          <entity name='lux_applicationendorsements'>
                            //                            <attribute name='lux_applicationendorsementsid' />
                            //                            <attribute name='lux_name' />
                            //                            <attribute name='lux_endorsementnumber' />
                            //                            <attribute name='createdon' />
                            //                            <order attribute='lux_name' descending='false' />
                            //                            <filter type='and'>
                            //                              <condition attribute='statecode' operator='eq' value='0' />
                            //                              <condition attribute='lux_endorsementlibrary' operator='eq' uiname='' uitype='lux_endorsementlibrary' value='{Endorsement7.Attributes["lux_endorsementlibraryid"]}' />
                            //                              <condition attribute='lux_application' operator='eq' uiname='' uitype='lux_propertyownersapplications' value='{appln.Id}' />
                            //                            </filter>
                            //                          </entity>
                            //                        </fetch>";
                            //        if ((SubsidienceScore == 4 || SubsidienceScore == 5) && item.GetAttributeValue<bool>("lux_issubsidencecoverrequired") == true) //£750 Flood Excess
                            //        {
                            //            if (service.RetrieveMultiple(new FetchExpression(fetch1)).Entities.Count == 0)
                            //            {
                            //                Entity ent = new Entity("lux_applicationendorsements");
                            //                ent["lux_isdefault"] = true;
                            //                ent["lux_endorsementnumber"] = Endorsement7.Attributes["new_endorsementnumber"];
                            //                ent["lux_endorsementhtml"] = Endorsement7.Attributes["lux_endorsementhtml"].ToString().Replace("XXXX", TexttoAppend);
                            //                ent["lux_name"] = Endorsement7.Attributes["lux_name"];
                            //                ent["lux_endorsementlibrary"] = new EntityReference("lux_endorsementlibrary", new Guid(Endorsement7.Attributes["lux_endorsementlibraryid"].ToString()));
                            //                ent["lux_application"] = new EntityReference("lux_propertyownersapplications", appln.Id);
                            //                service.Create(ent);
                            //            }
                            //        }
                            //        //else
                            //        //{
                            //        //    if (service.RetrieveMultiple(new FetchExpression(fetch1)).Entities.Count > 0)
                            //        //    {
                            //        //        service.Delete("lux_applicationendorsements", service.RetrieveMultiple(new FetchExpression(fetch1)).Entities[0].Id);
                            //        //    }
                            //        //}
                            //    }

                            //    var Endorsement8 = service.RetrieveMultiple(new FetchExpression(endorsementFetch)).Entities.FirstOrDefault(x => x.Attributes["lux_name"].ToString() == "Increased Subsidence Excess - £5,000");
                            //    if (Endorsement8 != null)
                            //    {
                            //        var TexttoAppend = "";
                            //        var premiseFetch = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                            //                          <entity name='lux_propertyownerspremise'>
                            //                            <attribute name='lux_riskpostcode' />
                            //                            <attribute name='lux_locationnumber' />
                            //                            <attribute name='lux_floodscore' />
                            //                            <attribute name='lux_subsidencescore' />
                            //                            <attribute name='lux_propertyownerspremiseid' />
                            //                            <attribute name='lux_riskaddress' />
                            //                            <attribute name='lux_housenumber' />
                            //                            <attribute name='lux_citycounty' />
                            //                            <order attribute='lux_locationnumber' descending='false' />
                            //                            <filter type='and'>
                            //                              <condition attribute='lux_subsidencescore' operator='eq' value='5' />
                            //                              <condition attribute='lux_propertyownersapplication' operator='eq' uiname='Landcage LLP' uitype='lux_propertyownersapplications' value='{appln.Id}' />
                            //                            </filter>
                            //                          </entity>
                            //                        </fetch>";

                            //        if (service.RetrieveMultiple(new FetchExpression(premiseFetch)).Entities.Count > 0)
                            //        {
                            //            var count = service.RetrieveMultiple(new FetchExpression(premiseFetch)).Entities.Count;
                            //            if (count == 1)
                            //            {
                            //                var premiseData = service.RetrieveMultiple(new FetchExpression(premiseFetch)).Entities[0];
                            //                var premiseNumber = premiseData.Contains("lux_locationnumber") ? premiseData.Attributes["lux_locationnumber"] : "";
                            //                var houseNumber = premiseData.Contains("lux_housenumber") ? premiseData.Attributes["lux_housenumber"] : "";
                            //                var street = premiseData.Contains("lux_riskaddress") ? premiseData.Attributes["lux_riskaddress"] : "";
                            //                var city = premiseData.Contains("lux_citycounty") ? premiseData.Attributes["lux_citycounty"] : "";
                            //                var postcode = premiseData.Contains("lux_riskpostcode") ? premiseData.Attributes["lux_riskpostcode"] : "";

                            //                TexttoAppend = "Premises " + premiseNumber + " - " + houseNumber + " " + street + " " + city + " " + postcode + " only.".Replace("  ", " ");
                            //            }
                            //            else if (count > 1)
                            //            {
                            //                foreach (var riskItem in service.RetrieveMultiple(new FetchExpression(premiseFetch)).Entities)
                            //                {
                            //                    var premiseNumber = riskItem.Contains("lux_locationnumber") ? riskItem.Attributes["lux_locationnumber"] : "";
                            //                    var houseNumber = riskItem.Contains("lux_housenumber") ? riskItem.Attributes["lux_housenumber"] : "";
                            //                    var street = riskItem.Contains("lux_riskaddress") ? riskItem.Attributes["lux_riskaddress"] : "";
                            //                    var city = riskItem.Contains("lux_citycounty") ? riskItem.Attributes["lux_citycounty"] : "";
                            //                    var postcode = riskItem.Contains("lux_riskpostcode") ? riskItem.Attributes["lux_riskpostcode"] : "";
                            //                    TexttoAppend += "<br>Premises " + premiseNumber + " - " + houseNumber + " " + street + " " + city + " " + postcode + "".Replace("  ", " ");
                            //                }
                            //            }
                            //        }

                            //        var fetch1 = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                            //                          <entity name='lux_applicationendorsements'>
                            //                            <attribute name='lux_applicationendorsementsid' />
                            //                            <attribute name='lux_name' />
                            //                            <attribute name='lux_endorsementnumber' />
                            //                            <attribute name='createdon' />
                            //                            <order attribute='lux_name' descending='false' />
                            //                            <filter type='and'>
                            //                              <condition attribute='statecode' operator='eq' value='0' />
                            //                              <condition attribute='lux_endorsementlibrary' operator='eq' uiname='' uitype='lux_endorsementlibrary' value='{Endorsement8.Attributes["lux_endorsementlibraryid"]}' />
                            //                              <condition attribute='lux_application' operator='eq' uiname='' uitype='lux_propertyownersapplications' value='{appln.Id}' />
                            //                            </filter>
                            //                          </entity>
                            //                        </fetch>";
                            //        if (SubsidienceScore == 6 && item.GetAttributeValue<bool>("lux_issubsidencecoverrequired") == true) //£750 Flood Excess
                            //        {
                            //            if (service.RetrieveMultiple(new FetchExpression(fetch1)).Entities.Count == 0)
                            //            {
                            //                Entity ent = new Entity("lux_applicationendorsements");
                            //                ent["lux_isdefault"] = true;
                            //                ent["lux_endorsementnumber"] = Endorsement8.Attributes["new_endorsementnumber"];
                            //                ent["lux_endorsementhtml"] = Endorsement8.Attributes["lux_endorsementhtml"].ToString().Replace("XXXX", TexttoAppend);
                            //                ent["lux_name"] = Endorsement8.Attributes["lux_name"];
                            //                ent["lux_endorsementlibrary"] = new EntityReference("lux_endorsementlibrary", new Guid(Endorsement8.Attributes["lux_endorsementlibraryid"].ToString()));
                            //                ent["lux_application"] = new EntityReference("lux_propertyownersapplications", appln.Id);
                            //                service.Create(ent);
                            //            }
                            //        }
                            //        //else
                            //        //{
                            //        //    if (service.RetrieveMultiple(new FetchExpression(fetch1)).Entities.Count > 0)
                            //        //    {
                            //        //        service.Delete("lux_applicationendorsements", service.RetrieveMultiple(new FetchExpression(fetch1)).Entities[0].Id);
                            //        //    }
                            //        //}
                            //    }
                            //}
                            //else
                            //{
                            var Endorsement7 = service.RetrieveMultiple(new FetchExpression(endorsementFetch)).Entities.FirstOrDefault(x => x.Attributes["lux_name"].ToString() == "Increased Subsidence Excess - £2,500");
                            if (Endorsement7 != null)
                            {
                                var TexttoAppend = "";
                                var premiseFetch = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                                                          <entity name='lux_propertyownerspremise'>
                                                            <attribute name='lux_riskpostcode' />
                                                            <attribute name='lux_locationnumber' />
                                                            <attribute name='lux_floodscore' />
                                                            <attribute name='lux_subsidencescore' />
                                                            <attribute name='lux_propertyownerspremiseid' />
                                                            <attribute name='lux_riskaddress' />
                                                            <attribute name='lux_housenumber' />
                                                            <attribute name='lux_citycounty' />
                                                            <order attribute='lux_locationnumber' descending='false' />
                                                            <filter type='and'>
                                                              <condition attribute='lux_subsidencescore' operator='eq' value='4' />
                                                              <condition attribute='lux_propertyownersapplication' operator='eq' uiname='' uitype='lux_propertyownersapplications' value='{appln.Id}' />
                                                            </filter>
                                                          </entity>
                                                        </fetch>";

                                if (service.RetrieveMultiple(new FetchExpression(premiseFetch)).Entities.Count > 0)
                                {
                                    var count = service.RetrieveMultiple(new FetchExpression(premiseFetch)).Entities.Count;
                                    if (count == 1)
                                    {
                                        var premiseData = service.RetrieveMultiple(new FetchExpression(premiseFetch)).Entities[0];
                                        var premiseNumber = premiseData.Contains("lux_locationnumber") ? premiseData.Attributes["lux_locationnumber"] : "";
                                        var houseNumber = premiseData.Contains("lux_housenumber") ? premiseData.Attributes["lux_housenumber"] : "";
                                        var street = premiseData.Contains("lux_riskaddress") ? premiseData.Attributes["lux_riskaddress"] : "";
                                        var city = premiseData.Contains("lux_citycounty") ? premiseData.Attributes["lux_citycounty"] : "";
                                        var postcode = premiseData.Contains("lux_riskpostcode") ? premiseData.Attributes["lux_riskpostcode"] : "";

                                        TexttoAppend = "Premises " + premiseNumber + " - " + houseNumber + " " + street + " " + city + " " + postcode + " only.".Replace("  ", " ");
                                    }
                                    else if (count > 1)
                                    {
                                        foreach (var riskItem in service.RetrieveMultiple(new FetchExpression(premiseFetch)).Entities)
                                        {
                                            var premiseNumber = riskItem.Contains("lux_locationnumber") ? riskItem.Attributes["lux_locationnumber"] : "";
                                            var houseNumber = riskItem.Contains("lux_housenumber") ? riskItem.Attributes["lux_housenumber"] : "";
                                            var street = riskItem.Contains("lux_riskaddress") ? riskItem.Attributes["lux_riskaddress"] : "";
                                            var city = riskItem.Contains("lux_citycounty") ? riskItem.Attributes["lux_citycounty"] : "";
                                            var postcode = riskItem.Contains("lux_riskpostcode") ? riskItem.Attributes["lux_riskpostcode"] : "";
                                            TexttoAppend += "<br>Premises " + premiseNumber + " - " + houseNumber + " " + street + " " + city + " " + postcode + "".Replace("  ", " ");
                                        }
                                    }
                                }

                                var fetch1 = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                                                          <entity name='lux_applicationendorsements'>
                                                            <attribute name='lux_applicationendorsementsid' />
                                                            <attribute name='lux_name' />
                                                            <attribute name='lux_endorsementnumber' />
                                                            <attribute name='createdon' />
                                                            <order attribute='lux_name' descending='false' />
                                                            <filter type='and'>
                                                              <condition attribute='statecode' operator='eq' value='0' />
                                                              <condition attribute='lux_endorsementlibrary' operator='eq' uiname='' uitype='lux_endorsementlibrary' value='{Endorsement7.Attributes["lux_endorsementlibraryid"]}' />
                                                              <condition attribute='lux_application' operator='eq' uiname='' uitype='lux_propertyownersapplications' value='{appln.Id}' />
                                                            </filter>
                                                          </entity>
                                                        </fetch>";
                                if ((SubsidienceScore == 6) && item.GetAttributeValue<bool>("lux_issubsidencecoverrequired") == true) //£750 Flood Excess
                                {
                                    if (service.RetrieveMultiple(new FetchExpression(fetch1)).Entities.Count == 0)
                                    {
                                        Entity ent = new Entity("lux_applicationendorsements");
                                        ent["lux_isdefault"] = true;
                                        ent["lux_endorsementnumber"] = Endorsement7.Attributes["new_endorsementnumber"];
                                        ent["lux_endorsementhtml"] = Endorsement7.Attributes["lux_endorsementhtml"].ToString().Replace("XXXX", TexttoAppend);
                                        ent["lux_name"] = Endorsement7.Attributes["lux_name"];
                                        ent["lux_endorsementlibrary"] = new EntityReference("lux_endorsementlibrary", new Guid(Endorsement7.Attributes["lux_endorsementlibraryid"].ToString()));
                                        ent["lux_application"] = new EntityReference("lux_propertyownersapplications", appln.Id);
                                        service.Create(ent);
                                    }
                                }
                            }
                            //}

                            var Endorsement9 = service.RetrieveMultiple(new FetchExpression(endorsementFetch)).Entities.FirstOrDefault(x => x.Attributes["lux_name"].ToString() == "Malicious Damage by Residential Tenant Restriction");
                            if (Endorsement9 != null)
                            {
                                var fetch1 = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                                                          <entity name='lux_applicationendorsements'>
                                                            <attribute name='lux_applicationendorsementsid' />
                                                            <attribute name='lux_name' />
                                                            <attribute name='lux_endorsementnumber' />
                                                            <attribute name='createdon' />
                                                            <order attribute='lux_name' descending='false' />
                                                            <filter type='and'>
                                                              <condition attribute='statecode' operator='eq' value='0' />
                                                              <condition attribute='lux_endorsementlibrary' operator='eq' uiname='Wood Burner Condition' uitype='lux_endorsementlibrary' value='{Endorsement9.Attributes["lux_endorsementlibraryid"]}' />
                                                              <condition attribute='lux_riskpremise' operator='eq' uiname='' uitype='lux_propertyownerspremise' value='{item.Id}' />
                                                            </filter>
                                                          </entity>
                                                        </fetch>";
                                //if (model.IsLive == 0)
                                //{
                                if ((OccupancyType == 972970002 || OccupancyType == 972970004) && (TenentType == 972970009 || TenentType == 972970003)) // Holiday Let
                                {
                                    if (service.RetrieveMultiple(new FetchExpression(fetch1)).Entities.Count == 0)
                                    {
                                        Entity ent = new Entity("lux_applicationendorsements");
                                        ent["lux_isdefault"] = true;
                                        ent["lux_endorsementnumber"] = Endorsement9.Attributes["new_endorsementnumber"];
                                        ent["lux_endorsementhtml"] = Endorsement9.Attributes["lux_endorsementhtml"];
                                        ent["lux_name"] = Endorsement9.Attributes["lux_name"];
                                        ent["lux_endorsementlibrary"] = new EntityReference("lux_endorsementlibrary", new Guid(Endorsement9.Attributes["lux_endorsementlibraryid"].ToString()));
                                        ent["lux_application"] = new EntityReference("lux_propertyownersapplications", appln.Id);
                                        service.Create(ent);
                                    }
                                }
                                //else
                                //{
                                //    if (service.RetrieveMultiple(new FetchExpression(fetch1)).Entities.Count > 0)
                                //    {
                                //        service.Delete("lux_applicationendorsements", service.RetrieveMultiple(new FetchExpression(fetch1)).Entities[0].Id);
                                //    }
                                //}
                                //}
                                //else
                                //{
                                //    if ((OccupancyType == 972970002 || OccupancyType == 972970004) && TenentType == 972970009) // Holiday Let
                                //    {
                                //        if (service.RetrieveMultiple(new FetchExpression(fetch1)).Entities.Count == 0)
                                //        {
                                //            Entity ent = new Entity("lux_applicationendorsements");
                                //            ent["lux_isdefault"] = true;
                                //            ent["lux_endorsementnumber"] = Endorsement9.Attributes["new_endorsementnumber"];
                                //            ent["lux_endorsementhtml"] = Endorsement9.Attributes["lux_endorsementhtml"];
                                //            ent["lux_name"] = Endorsement9.Attributes["lux_name"];
                                //            ent["lux_endorsementlibrary"] = new EntityReference("lux_endorsementlibrary", new Guid(Endorsement9.Attributes["lux_endorsementlibraryid"].ToString()));
                                //            ent["lux_application"] = new EntityReference("lux_propertyownersapplications", appln.Id);
                                //            service.Create(ent);
                                //        }
                                //    }
                                //    //else
                                //    //{
                                //    //    if (service.RetrieveMultiple(new FetchExpression(fetch1)).Entities.Count > 0)
                                //    //    {
                                //    //        service.Delete("lux_applicationendorsements", service.RetrieveMultiple(new FetchExpression(fetch1)).Entities[0].Id);
                                //    //    }
                                //    //}
                                //}
                            }

                            var Endorsement10 = service.RetrieveMultiple(new FetchExpression(endorsementFetch)).Entities.FirstOrDefault(x => x.Attributes["lux_name"].ToString() == "Subsidence Exclusion");
                            if (Endorsement10 != null)
                            {
                                var TexttoAppend = "";
                                var premiseFetch = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                                                          <entity name='lux_propertyownerspremise'>
                                                            <attribute name='lux_riskpostcode' />
                                                            <attribute name='lux_locationnumber' />
                                                            <attribute name='lux_floodscore' />
                                                            <attribute name='lux_subsidencescore' />
                                                            <attribute name='lux_propertyownerspremiseid' />
                                                            <attribute name='lux_riskaddress' />
                                                            <attribute name='lux_housenumber' />
                                                            <attribute name='lux_citycounty' />
                                                            <order attribute='lux_locationnumber' descending='false' />
                                                            <filter type='and'>
                                                              <condition attribute='lux_subsidencescore' operator='eq' value='7' />
                                                              <condition attribute='lux_propertyownersapplication' operator='eq' uiname='Landcage LLP' uitype='lux_propertyownersapplications' value='{appln.Id}' />
                                                            </filter>
                                                          </entity>
                                                        </fetch>";

                                if (service.RetrieveMultiple(new FetchExpression(premiseFetch)).Entities.Count > 0)
                                {
                                    var count = service.RetrieveMultiple(new FetchExpression(premiseFetch)).Entities.Count;
                                    if (count == 1)
                                    {
                                        var premiseData = service.RetrieveMultiple(new FetchExpression(premiseFetch)).Entities[0];
                                        var premiseNumber = premiseData.Contains("lux_locationnumber") ? premiseData.Attributes["lux_locationnumber"] : "";
                                        var houseNumber = premiseData.Contains("lux_housenumber") ? premiseData.Attributes["lux_housenumber"] : "";
                                        var street = premiseData.Contains("lux_riskaddress") ? premiseData.Attributes["lux_riskaddress"] : "";
                                        var city = premiseData.Contains("lux_citycounty") ? premiseData.Attributes["lux_citycounty"] : "";
                                        var postcode = premiseData.Contains("lux_riskpostcode") ? premiseData.Attributes["lux_riskpostcode"] : "";

                                        TexttoAppend = "Premises " + premiseNumber + " - " + houseNumber + " " + street + " " + city + " " + postcode + " only.".Replace("  ", " ");
                                    }
                                    else if (count > 1)
                                    {
                                        foreach (var riskItem in service.RetrieveMultiple(new FetchExpression(premiseFetch)).Entities)
                                        {
                                            var premiseNumber = riskItem.Contains("lux_locationnumber") ? riskItem.Attributes["lux_locationnumber"] : "";
                                            var houseNumber = riskItem.Contains("lux_housenumber") ? riskItem.Attributes["lux_housenumber"] : "";
                                            var street = riskItem.Contains("lux_riskaddress") ? riskItem.Attributes["lux_riskaddress"] : "";
                                            var city = riskItem.Contains("lux_citycounty") ? riskItem.Attributes["lux_citycounty"] : "";
                                            var postcode = riskItem.Contains("lux_riskpostcode") ? riskItem.Attributes["lux_riskpostcode"] : "";
                                            TexttoAppend += "<br>Premises " + premiseNumber + " - " + houseNumber + " " + street + " " + city + " " + postcode + "".Replace("  ", " ");
                                        }
                                    }
                                }

                                var fetch1 = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                                                          <entity name='lux_applicationendorsements'>
                                                            <attribute name='lux_applicationendorsementsid' />
                                                            <attribute name='lux_name' />
                                                            <attribute name='lux_endorsementnumber' />
                                                            <attribute name='createdon' />
                                                            <order attribute='lux_name' descending='false' />
                                                            <filter type='and'>
                                                              <condition attribute='statecode' operator='eq' value='0' />
                                                              <condition attribute='lux_endorsementlibrary' operator='eq' uiname='' uitype='lux_endorsementlibrary' value='{Endorsement10.Attributes["lux_endorsementlibraryid"]}' />
                                                              <condition attribute='lux_application' operator='eq' uiname='' uitype='lux_propertyownersapplications' value='{appln.Id}' />
                                                            </filter>
                                                          </entity>
                                                        </fetch>";
                                if (SubsidienceScore == 7 && item.GetAttributeValue<bool>("lux_issubsidencecoverrequired") == true) //£750 Flood Excess
                                {
                                    if (service.RetrieveMultiple(new FetchExpression(fetch1)).Entities.Count == 0)
                                    {
                                        Entity ent = new Entity("lux_applicationendorsements");
                                        ent["lux_isdefault"] = true;
                                        ent["lux_endorsementnumber"] = Endorsement10.Attributes["new_endorsementnumber"];
                                        ent["lux_endorsementhtml"] = Endorsement10.Attributes["lux_endorsementhtml"].ToString().Replace("XXXX", TexttoAppend);
                                        ent["lux_name"] = Endorsement10.Attributes["lux_name"];
                                        ent["lux_endorsementlibrary"] = new EntityReference("lux_endorsementlibrary", new Guid(Endorsement10.Attributes["lux_endorsementlibraryid"].ToString()));
                                        ent["lux_application"] = new EntityReference("lux_propertyownersapplications", appln.Id);
                                        service.Create(ent);
                                    }
                                }
                                //else
                                //{
                                //    if (service.RetrieveMultiple(new FetchExpression(fetch1)).Entities.Count > 0)
                                //    {
                                //        service.Delete("lux_applicationendorsements", service.RetrieveMultiple(new FetchExpression(fetch1)).Entities[0].Id);
                                //    }
                                //}
                            }

                            var Endorsement11 = service.RetrieveMultiple(new FetchExpression(endorsementFetch)).Entities.FirstOrDefault(x => x.Attributes["lux_name"].ToString() == "Holiday Lets Condition");
                            if (Endorsement11 != null)
                            {
                                var fetch1 = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                                                          <entity name='lux_applicationendorsements'>
                                                            <attribute name='lux_applicationendorsementsid' />
                                                            <attribute name='lux_name' />
                                                            <attribute name='lux_endorsementnumber' />
                                                            <attribute name='createdon' />
                                                            <order attribute='lux_name' descending='false' />
                                                            <filter type='and'>
                                                              <condition attribute='statecode' operator='eq' value='0' />
                                                              <condition attribute='lux_endorsementlibrary' operator='eq' uiname='' uitype='lux_endorsementlibrary' value='{Endorsement11.Attributes["lux_endorsementlibraryid"]}' />
                                                              <condition attribute='lux_application' operator='eq' uiname='' uitype='lux_propertyownersapplications' value='{appln.Id}' />
                                                            </filter>
                                                          </entity>
                                                        </fetch>";
                                if (((OccupancyType == 972970002 || OccupancyType == 972970004)) && TenentType == 972970009) // Holiday Let
                                {
                                    if (service.RetrieveMultiple(new FetchExpression(fetch1)).Entities.Count == 0)
                                    {
                                        Entity ent = new Entity("lux_applicationendorsements");
                                        ent["lux_isdefault"] = true;
                                        ent["lux_endorsementnumber"] = Endorsement11.Attributes["new_endorsementnumber"];
                                        ent["lux_endorsementhtml"] = Endorsement11.Attributes["lux_endorsementhtml"];
                                        ent["lux_name"] = Endorsement11.Attributes["lux_name"];
                                        ent["lux_endorsementlibrary"] = new EntityReference("lux_endorsementlibrary", new Guid(Endorsement11.Attributes["lux_endorsementlibraryid"].ToString()));
                                        ent["lux_application"] = new EntityReference("lux_propertyownersapplications", appln.Id);
                                        service.Create(ent);
                                    }
                                }
                                //else
                                //{
                                //    if (service.RetrieveMultiple(new FetchExpression(fetch1)).Entities.Count > 0)
                                //    {
                                //        service.Delete("lux_applicationendorsements", service.RetrieveMultiple(new FetchExpression(fetch1)).Entities[0].Id);
                                //    }
                                //}
                            }

                            var Endorsement12 = service.RetrieveMultiple(new FetchExpression(endorsementFetch)).Entities.FirstOrDefault(x => x.Attributes["lux_name"].ToString() == "Electrical Inspection and Testing Programme");
                            if (Endorsement12 != null)
                            {
                                var fetch1 = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                                                          <entity name='lux_applicationendorsements'>
                                                            <attribute name='lux_applicationendorsementsid' />
                                                            <attribute name='lux_name' />
                                                            <attribute name='lux_endorsementnumber' />
                                                            <attribute name='createdon' />
                                                            <order attribute='lux_name' descending='false' />
                                                            <filter type='and'>
                                                              <condition attribute='statecode' operator='eq' value='0' />
                                                              <condition attribute='lux_endorsementlibrary' operator='eq' uiname='' uitype='lux_endorsementlibrary' value='{Endorsement12.Attributes["lux_endorsementlibraryid"]}' />
                                                              <condition attribute='lux_application' operator='eq' uiname='' uitype='lux_propertyownersapplications' value='{appln.Id}' />
                                                            </filter>
                                                          </entity>
                                                        </fetch>";

                                if (service.RetrieveMultiple(new FetchExpression(fetch1)).Entities.Count == 0)
                                {
                                    Entity ent = new Entity("lux_applicationendorsements");
                                    ent["lux_isdefault"] = true;
                                    ent["lux_endorsementnumber"] = Endorsement12.Attributes["new_endorsementnumber"];
                                    ent["lux_endorsementhtml"] = Endorsement12.Attributes["lux_endorsementhtml"];
                                    ent["lux_name"] = Endorsement12.Attributes["lux_name"];
                                    ent["lux_endorsementlibrary"] = new EntityReference("lux_endorsementlibrary", new Guid(Endorsement12.Attributes["lux_endorsementlibraryid"].ToString()));
                                    ent["lux_application"] = new EntityReference("lux_propertyownersapplications", appln.Id);
                                    service.Create(ent);
                                }
                            }

                            var Endorsement13 = service.RetrieveMultiple(new FetchExpression(endorsementFetch)).Entities.FirstOrDefault(x => x.Attributes["lux_name"].ToString() == "House of Multiple Occupancy Extension");
                            if (Endorsement13 != null)
                            {
                                var fetch1 = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                                                          <entity name='lux_applicationendorsements'>
                                                            <attribute name='lux_applicationendorsementsid' />
                                                            <attribute name='lux_name' />
                                                            <attribute name='lux_endorsementnumber' />
                                                            <attribute name='createdon' />
                                                            <order attribute='lux_name' descending='false' />
                                                            <filter type='and'>
                                                              <condition attribute='statecode' operator='eq' value='0' />
                                                              <condition attribute='lux_endorsementlibrary' operator='eq' uiname='' uitype='lux_endorsementlibrary' value='{Endorsement13.Attributes["lux_endorsementlibraryid"]}' />
                                                              <condition attribute='lux_application' operator='eq' uiname='' uitype='lux_propertyownersapplications' value='{appln.Id}' />
                                                            </filter>
                                                          </entity>
                                                        </fetch>";

                                if (IsHMO == true) // HMO
                                {
                                    if (service.RetrieveMultiple(new FetchExpression(fetch1)).Entities.Count == 0)
                                    {
                                        Entity ent = new Entity("lux_applicationendorsements");
                                        ent["lux_isdefault"] = true;
                                        ent["lux_endorsementnumber"] = Endorsement13.Attributes["new_endorsementnumber"];
                                        ent["lux_endorsementhtml"] = Endorsement13.Attributes["lux_endorsementhtml"];
                                        ent["lux_name"] = Endorsement13.Attributes["lux_name"];
                                        ent["lux_endorsementlibrary"] = new EntityReference("lux_endorsementlibrary", new Guid(Endorsement13.Attributes["lux_endorsementlibraryid"].ToString()));
                                        ent["lux_application"] = new EntityReference("lux_propertyownersapplications", appln.Id);
                                        service.Create(ent);
                                    }
                                }
                                //else
                                //{
                                //    if (service.RetrieveMultiple(new FetchExpression(fetch1)).Entities.Count > 0)
                                //    {
                                //        service.Delete("lux_applicationendorsements", service.RetrieveMultiple(new FetchExpression(fetch1)).Entities[0].Id);
                                //    }
                                //}
                            }

                            var Endorsement14 = service.RetrieveMultiple(new FetchExpression(endorsementFetch)).Entities.FirstOrDefault(x => x.Attributes["lux_name"].ToString() == "Increased Malicious Damage by Tenant Excess - £500");
                            if (Endorsement14 != null)
                            {
                                var fetch1 = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                                                          <entity name='lux_applicationendorsements'>
                                                            <attribute name='lux_applicationendorsementsid' />
                                                            <attribute name='lux_name' />
                                                            <attribute name='lux_endorsementnumber' />
                                                            <attribute name='createdon' />
                                                            <order attribute='lux_name' descending='false' />
                                                            <filter type='and'>
                                                              <condition attribute='statecode' operator='eq' value='0' />
                                                              <condition attribute='lux_endorsementlibrary' operator='eq' uiname='' uitype='lux_endorsementlibrary' value='{Endorsement14.Attributes["lux_endorsementlibraryid"]}' />
                                                              <condition attribute='lux_application' operator='eq' uiname='' uitype='lux_propertyownersapplications' value='{appln.Id}' />
                                                            </filter>
                                                          </entity>
                                                        </fetch>";
                                if ((OccupancyType == 972970002 || OccupancyType == 972970004) && (TenentType == 972970004 || TenentType == 972970007 || TenentType == 972970012)) // Asylum Seekers, Local Authority or Charity Lets
                                {
                                    if (service.RetrieveMultiple(new FetchExpression(fetch1)).Entities.Count == 0)
                                    {
                                        Entity ent = new Entity("lux_applicationendorsements");
                                        ent["lux_isdefault"] = true;
                                        ent["lux_endorsementnumber"] = Endorsement14.Attributes["new_endorsementnumber"];
                                        ent["lux_endorsementhtml"] = Endorsement14.Attributes["lux_endorsementhtml"];
                                        ent["lux_name"] = Endorsement14.Attributes["lux_name"];
                                        ent["lux_endorsementlibrary"] = new EntityReference("lux_endorsementlibrary", new Guid(Endorsement14.Attributes["lux_endorsementlibraryid"].ToString()));
                                        ent["lux_application"] = new EntityReference("lux_propertyownersapplications", appln.Id);
                                        service.Create(ent);
                                    }
                                }
                                //else
                                //{
                                //    if (service.RetrieveMultiple(new FetchExpression(fetch1)).Entities.Count > 0)
                                //    {
                                //        service.Delete("lux_applicationendorsements", service.RetrieveMultiple(new FetchExpression(fetch1)).Entities[0].Id);
                                //    }
                                //}
                            }

                            var Endorsement15 = service.RetrieveMultiple(new FetchExpression(endorsementFetch)).Entities.FirstOrDefault(x => x.Attributes["lux_name"].ToString() == "Serviced Apartments Condition");
                            if (Endorsement15 != null)
                            {
                                var fetch1 = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                                                          <entity name='lux_applicationendorsements'>
                                                            <attribute name='lux_applicationendorsementsid' />
                                                            <attribute name='lux_name' />
                                                            <attribute name='lux_endorsementnumber' />
                                                            <attribute name='createdon' />
                                                            <order attribute='lux_name' descending='false' />
                                                            <filter type='and'>
                                                              <condition attribute='statecode' operator='eq' value='0' />
                                                              <condition attribute='lux_endorsementlibrary' operator='eq' uiname='' uitype='lux_endorsementlibrary' value='{Endorsement15.Attributes["lux_endorsementlibraryid"]}' />
                                                              <condition attribute='lux_application' operator='eq' uiname='' uitype='lux_propertyownersapplications' value='{appln.Id}' />
                                                            </filter>
                                                          </entity>
                                                        </fetch>";
                                if (TenentType == 972970010) // Serviced Apartments
                                {
                                    if (service.RetrieveMultiple(new FetchExpression(fetch1)).Entities.Count == 0)
                                    {
                                        Entity ent = new Entity("lux_applicationendorsements");
                                        ent["lux_isdefault"] = true;
                                        ent["lux_endorsementnumber"] = Endorsement15.Attributes["new_endorsementnumber"];
                                        ent["lux_endorsementhtml"] = Endorsement15.Attributes["lux_endorsementhtml"];
                                        ent["lux_name"] = Endorsement15.Attributes["lux_name"];
                                        ent["lux_endorsementlibrary"] = new EntityReference("lux_endorsementlibrary", new Guid(Endorsement15.Attributes["lux_endorsementlibraryid"].ToString()));
                                        ent["lux_application"] = new EntityReference("lux_propertyownersapplications", appln.Id);
                                        service.Create(ent);
                                    }
                                }
                                //else
                                //{
                                //    if (service.RetrieveMultiple(new FetchExpression(fetch1)).Entities.Count > 0)
                                //    {
                                //        service.Delete("lux_applicationendorsements", service.RetrieveMultiple(new FetchExpression(fetch1)).Entities[0].Id);
                                //    }
                                //}
                            }

                            //var BrokerName = item.Attributes.Contains("poa.lux_broker") ? item.FormattedValues["poa.lux_broker"].ToString() : "";
                            var Agency = "";
                            if (item.Attributes.Contains("poa.lux_broker"))
                            {
                                var Broker = service.Retrieve("account", ((EntityReference)((item.GetAttributeValue<AliasedValue>("poa.lux_broker")).Value)).Id, new ColumnSet("lux_agencynumber"));
                                Agency = Broker.Attributes["lux_agencynumber"].ToString();
                            }
                            //if (BrokerName == "Clear Insurance Management (Toogoods)")
                            if (Agency == "ASU005")
                            {
                                var Endorsement16 = service.RetrieveMultiple(new FetchExpression(endorsementFetch)).Entities.FirstOrDefault(x => x.Attributes["lux_name"].ToString() == "Amendment to Unoccupied buildings cover restrictions – residential premises");
                                if (Endorsement16 != null)
                                {
                                    var fetch1 = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                                                          <entity name='lux_applicationendorsements'>
                                                            <attribute name='lux_applicationendorsementsid' />
                                                            <attribute name='lux_name' />
                                                            <attribute name='lux_endorsementnumber' />
                                                            <attribute name='createdon' />
                                                            <order attribute='lux_name' descending='false' />
                                                            <filter type='and'>
                                                              <condition attribute='statecode' operator='eq' value='0' />
                                                              <condition attribute='lux_endorsementlibrary' operator='eq' uiname='' uitype='lux_endorsementlibrary' value='{Endorsement16.Attributes["lux_endorsementlibraryid"]}' />
                                                              <condition attribute='lux_application' operator='eq' uiname='' uitype='lux_propertyownersapplications' value='{appln.Id}' />
                                                            </filter>
                                                          </entity>
                                                        </fetch>";

                                    if (POProuctType == 972970001) // Residential
                                    {
                                        if (service.RetrieveMultiple(new FetchExpression(fetch1)).Entities.Count == 0)
                                        {
                                            Entity ent = new Entity("lux_applicationendorsements");
                                            ent["lux_isdefault"] = true;
                                            ent["lux_endorsementhtml"] = Endorsement16.Attributes["lux_endorsementhtml"];
                                            ent["lux_endorsementnumber"] = Endorsement16.Attributes["new_endorsementnumber"];
                                            ent["lux_name"] = Endorsement16.Attributes["lux_name"];
                                            ent["lux_endorsementlibrary"] = new EntityReference("lux_endorsementlibrary", new Guid(Endorsement16.Attributes["lux_endorsementlibraryid"].ToString()));
                                            ent["lux_application"] = new EntityReference("lux_propertyownersapplications", appln.Id);
                                            service.Create(ent);
                                        }
                                    }
                                }
                            }

                            //if (model.IsLive == 0)
                            //{
                            var Endorsement17 = service.RetrieveMultiple(new FetchExpression(endorsementFetch)).Entities.FirstOrDefault(x => x.Attributes["lux_name"].ToString() == "Flat Roof - Increased Excess");
                            if (Endorsement17 != null)
                            {
                                var percent = item.Attributes.Contains("lux_flatroofpercentage") ? item.Attributes["lux_flatroofpercentage"].ToString().Replace("%", "") : "0";
                                percent = Regex.Match(percent, @"\d+").Value;

                                var fetch1 = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                                                          <entity name='lux_applicationendorsements'>
                                                            <attribute name='lux_applicationendorsementsid' />
                                                            <attribute name='lux_name' />
                                                            <attribute name='lux_endorsementnumber' />
                                                            <attribute name='createdon' />
                                                            <order attribute='lux_name' descending='false' />
                                                            <filter type='and'>
                                                              <condition attribute='statecode' operator='eq' value='0' />
                                                              <condition attribute='lux_endorsementlibrary' operator='eq' uiname='Wood Burner Condition' uitype='lux_endorsementlibrary' value='{Endorsement17.Attributes["lux_endorsementlibraryid"]}' />
                                                              <condition attribute='lux_application' operator='eq' uiname='' uitype='lux_propertyownersapplications' value='{appln.Id}' />
                                                            </filter>
                                                          </entity>
                                                        </fetch>";

                                if (FlatRoof == true && Convert.ToDecimal(percent) > 50) // Flat roof
                                {
                                    if (service.RetrieveMultiple(new FetchExpression(fetch1)).Entities.Count == 0)
                                    {
                                        Entity ent = new Entity("lux_applicationendorsements");
                                        ent["lux_isdefault"] = true;
                                        ent["lux_endorsementhtml"] = Endorsement17.Attributes["lux_endorsementhtml"];
                                        ent["lux_endorsementnumber"] = Endorsement17.Attributes["new_endorsementnumber"];
                                        ent["lux_name"] = Endorsement17.Attributes["lux_name"];
                                        ent["lux_endorsementlibrary"] = new EntityReference("lux_endorsementlibrary", new Guid(Endorsement17.Attributes["lux_endorsementlibraryid"].ToString()));
                                        ent["lux_application"] = new EntityReference("lux_propertyownersapplications", appln.Id);

                                        service.Create(ent);
                                    }
                                }
                                //else
                                //{
                                //    if (service.RetrieveMultiple(new FetchExpression(fetch1)).Entities.Count > 0)
                                //    {
                                //        service.Delete("lux_applicationendorsements", service.RetrieveMultiple(new FetchExpression(fetch1)).Entities[0].Id);
                                //    }
                                //}
                            }

                            var Endorsement18 = service.RetrieveMultiple(new FetchExpression(endorsementFetch)).Entities.FirstOrDefault(x => x.Attributes["lux_name"].ToString() == "Increased Escape of Water Excess - £500");
                            if (Endorsement18 != null)
                            {
                                var fetch1 = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                                                          <entity name='lux_applicationendorsements'>
                                                            <attribute name='lux_applicationendorsementsid' />
                                                            <attribute name='lux_name' />
                                                            <attribute name='lux_endorsementnumber' />
                                                            <attribute name='createdon' />
                                                            <order attribute='lux_name' descending='false' />
                                                            <filter type='and'>
                                                              <condition attribute='statecode' operator='eq' value='0' />
                                                              <condition attribute='lux_endorsementlibrary' operator='eq' uiname='' uitype='lux_endorsementlibrary' value='{Endorsement18.Attributes["lux_endorsementlibraryid"]}' />
                                                              <condition attribute='lux_application' operator='eq' uiname='' uitype='lux_propertyownersapplications' value='{appln.Id}' />
                                                            </filter>
                                                          </entity>
                                                        </fetch>";

                                if (((OccupancyType == 972970002 || OccupancyType == 972970004) && TenentType == 972970003) || (IsHMO == true && (NoofBedrooms > 5 || HMOLicense == false || AnyCoockingFacility == true))) // Residential and Student
                                {
                                    if (service.RetrieveMultiple(new FetchExpression(fetch1)).Entities.Count == 0)
                                    {
                                        Entity ent = new Entity("lux_applicationendorsements");
                                        ent["lux_isdefault"] = true;
                                        ent["lux_endorsementnumber"] = Endorsement18.Attributes["new_endorsementnumber"];
                                        ent["lux_endorsementhtml"] = Endorsement18.Attributes["lux_endorsementhtml"];
                                        ent["lux_name"] = Endorsement18.Attributes["lux_name"];
                                        ent["lux_endorsementlibrary"] = new EntityReference("lux_endorsementlibrary", new Guid(Endorsement18.Attributes["lux_endorsementlibraryid"].ToString()));
                                        ent["lux_application"] = new EntityReference("lux_propertyownersapplications", appln.Id);
                                        service.Create(ent);
                                    }
                                }
                                //else
                                //{
                                //    if (service.RetrieveMultiple(new FetchExpression(fetch1)).Entities.Count > 0)
                                //    {
                                //        service.Delete("lux_applicationendorsements", service.RetrieveMultiple(new FetchExpression(fetch1)).Entities[0].Id);
                                //    }
                                //}
                            }
                            //}
                        }
                    }
                }
            }
            else if (productName == "Retail")
            {
                var fetch = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                                      <entity name='lux_propertyownersretail'>
                                        <attribute name='lux_propertyownersretailid' />
                                        <attribute name='lux_name' />
                                        <attribute name='createdon' />
                                        <attribute name='lux_rentreceivable' />
                                        <attribute name='lux_icow' />
                                        <attribute name='lux_bookdebts' />
                                        <attribute name='lux_amount' />
                                        <attribute name='lux_maintradeforthispremises' />
                                        <attribute name='lux_doesthepremiseshaveaflatroof' />
                                        <attribute name='lux_flatroofpercentage' />
                                        <attribute name='lux_doesthepremiseshaveabasement' />
                                        <attribute name='lux_isthereanatm' />            
                                        <attribute name='lux_issubsidencecoverrequired' />
                                        <attribute name='lux_isthereanintruderalarminstalled' />                                
                                        <attribute name='lux_whattypeofalarmisinstalled' />
                                        <attribute name='lux_isownerresponsibleforfillingcashmachine' />
                                        <attribute name='lux_anydffequipmentused' />
                                        <attribute name='lux_additionalincreasedcostofworking' />
                                        <attribute name='lux_floodscore' />
                                        <attribute name='lux_subsidencescore' />
                                        <attribute name='lux_crimescore' />
                                        <attribute name='lux_securityrating' />
                                        <order attribute='lux_name' descending='false' />
                                        <filter type='and'>
                                          <condition attribute='lux_propertyownersapplications' operator='eq' uiname='' uitype='lux_propertyownersapplications' value='{appln.Id}' />
                                        </filter>
                                        <link-entity name='lux_propertyownersapplications' from='lux_propertyownersapplicationsid' to='lux_propertyownersapplications' visible='false' link-type='outer' alias='appln'>
                                          <attribute name='lux_anyheatworkawayundertaken' />
                                          <attribute name='lux_isanyworkawaycarriedoutotherthanforcollec' />
                                          <attribute name='lux_maintradeforthispremises' />
                                          <attribute name='lux_secondarytradeofthebusiness' />
                                        </link-entity>
                                      </entity>
                                    </fetch>";

                if (service.RetrieveMultiple(new FetchExpression(fetch)).Entities.Count > 0)
                {
                    var endorsementFetch = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                                                          <entity name='lux_endorsementlibrary'>
                                                            <attribute name='lux_name' />
                                                            <attribute name='new_product' />
                                                            <attribute name='lux_insurer' />
                                                            <attribute name='lux_endorsementdescription' />
                                                            <attribute name='lux_endorsementhtml' />
                                                            <attribute name='new_endorsementnumber' />
                                                            <attribute name='lux_endorsementlibraryid' />
                                                            <order attribute='lux_name' descending='false' />
                                                            <filter type='and'>
                                                              <condition attribute='statecode' operator='eq' value='0' />
                                                              <condition attribute='lux_name' operator='not-null' />
                                                              <filter type='or'>
                                                                <condition attribute='new_product' operator='eq' uiname='Retail' uitype='product' value='{"e9cadb06-a496-eb11-b1ac-002248413665"}' />
                                                                <condition attribute='new_product' operator='null' />
                                                              </filter>
                                                            </filter>
                                                          </entity>
                                                        </fetch>";

                    foreach (var item in service.RetrieveMultiple(new FetchExpression(fetch)).Entities)
                    {
                        var FlatRoof = item.Attributes.Contains("lux_doesthepremiseshaveaflatroof") ? item.GetAttributeValue<bool>("lux_doesthepremiseshaveaflatroof") : false;
                        var ATM = item.Attributes.Contains("lux_isthereanatm") ? item.GetAttributeValue<bool>("lux_isthereanatm") : false;
                        var OwnerResponsible = item.Attributes.Contains("lux_isownerresponsibleforfillingcashmachine") ? item.GetAttributeValue<bool>("lux_isownerresponsibleforfillingcashmachine") : false;
                        var AnyDFF = item.Attributes.Contains("lux_anydffequipmentused") ? item.GetAttributeValue<bool>("lux_anydffequipmentused") : false;
                        var AnyHeatWorkAway = item.Attributes.Contains("appln.lux_anyheatworkawayundertaken") ? ((bool)(item.GetAttributeValue<AliasedValue>("appln.lux_anyheatworkawayundertaken").Value)) : false;
                        var WorkAway = item.Attributes.Contains("appln.lux_isanyworkawaycarriedoutotherthanforcollec") ? ((bool)(item.GetAttributeValue<AliasedValue>("appln.lux_isanyworkawaycarriedoutotherthanforcollec").Value)) : false;
                        var IntruderAlarm = item.Attributes.Contains("lux_isthereanintruderalarminstalled") ? item.GetAttributeValue<bool>("lux_isthereanintruderalarminstalled") : false;
                        var TypeOFAlarm = item.Attributes.Contains("lux_whattypeofalarmisinstalled") ? item.GetAttributeValue<OptionSetValue>("lux_whattypeofalarmisinstalled").Value : 0;
                        var PrimaryTrade = item.Attributes.Contains("appln.lux_maintradeforthispremises") ? item.FormattedValues["appln.lux_maintradeforthispremises"].ToString() : "";
                        var SecondaryTrade = item.Attributes.Contains("appln.lux_secondarytradeofthebusiness") ? item.FormattedValues["appln.lux_secondarytradeofthebusiness"].ToString() : "";
                        var Basement = item.Attributes.Contains("lux_doesthepremiseshaveabasement") ? item.GetAttributeValue<bool>("lux_doesthepremiseshaveabasement") : false;

                        if (service.RetrieveMultiple(new FetchExpression(endorsementFetch)).Entities.Count > 0)
                        {
                            var Endorsement1 = new Entity();
                            if (OwnerResponsible == true)
                            {
                                Endorsement1 = service.RetrieveMultiple(new FetchExpression(endorsementFetch)).Entities.FirstOrDefault(x => x.Attributes["lux_name"].ToString() == "Automated Teller Machine (ATM) - Self Fill");
                            }
                            else
                            {
                                Endorsement1 = service.RetrieveMultiple(new FetchExpression(endorsementFetch)).Entities.FirstOrDefault(x => x.Attributes["lux_name"].ToString() == "Automated Teller Machine (ATM) - Third Party Provided & Maintained Machines");
                            }

                            if (Endorsement1 != null)
                            {
                                var fetch1 = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                                                          <entity name='lux_applicationendorsements'>
                                                            <attribute name='lux_applicationendorsementsid' />
                                                            <attribute name='lux_name' />
                                                            <attribute name='lux_endorsementnumber' />
                                                            <attribute name='createdon' />
                                                            <order attribute='lux_name' descending='false' />
                                                            <filter type='and'>
                                                              <condition attribute='statecode' operator='eq' value='0' />
                                                              <condition attribute='lux_endorsementlibrary' operator='eq' uiname='' uitype='lux_endorsementlibrary' value='{Endorsement1.Attributes["lux_endorsementlibraryid"]}' />
                                                              <condition attribute='lux_application' operator='eq' uiname='' uitype='lux_propertyownersapplications' value='{appln.Id}' />
                                                            </filter>
                                                          </entity>
                                                        </fetch>";

                                if (ATM == true) // Flat roof
                                {
                                    if (service.RetrieveMultiple(new FetchExpression(fetch1)).Entities.Count == 0)
                                    {
                                        Entity ent = new Entity("lux_applicationendorsements");
                                        ent["lux_isdefault"] = true;
                                        ent["lux_endorsementhtml"] = Endorsement1.Attributes.Contains("lux_endorsementhtml") ? Endorsement1.Attributes["lux_endorsementhtml"] : "";
                                        ent["lux_endorsementnumber"] = Endorsement1.Attributes["new_endorsementnumber"];
                                        ent["lux_name"] = Endorsement1.Attributes["lux_name"];
                                        ent["lux_endorsementlibrary"] = new EntityReference("lux_endorsementlibrary", new Guid(Endorsement1.Attributes["lux_endorsementlibraryid"].ToString()));
                                        ent["lux_application"] = new EntityReference("lux_propertyownersapplications", appln.Id);
                                        service.Create(ent);
                                    }
                                }
                                //else
                                //{
                                //    if (service.RetrieveMultiple(new FetchExpression(fetch1)).Entities.Count > 0)
                                //    {
                                //        service.Delete("lux_applicationendorsements", service.RetrieveMultiple(new FetchExpression(fetch1)).Entities[0].Id);
                                //    }
                                //}
                            }

                            var Endorsement2 = service.RetrieveMultiple(new FetchExpression(endorsementFetch)).Entities.FirstOrDefault(x => x.Attributes["lux_name"].ToString() == "Kitchen Precautions");
                            if (Endorsement2 != null)
                            {
                                var fetch1 = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                                                          <entity name='lux_applicationendorsements'>
                                                            <attribute name='lux_applicationendorsementsid' />
                                                            <attribute name='lux_name' />
                                                            <attribute name='lux_endorsementnumber' />
                                                            <attribute name='createdon' />
                                                            <order attribute='lux_name' descending='false' />
                                                            <filter type='and'>
                                                              <condition attribute='statecode' operator='eq' value='0' />
                                                              <condition attribute='lux_endorsementlibrary' operator='eq' uiname='' uitype='lux_endorsementlibrary' value='{Endorsement2.Attributes["lux_endorsementlibraryid"]}' />
                                                              <condition attribute='lux_application' operator='eq' uiname='' uitype='lux_propertyownersapplications' value='{appln.Id}' />
                                                            </filter>
                                                          </entity>
                                                        </fetch>";

                                if (AnyDFF == true || (PrimaryTrade == "Fast Food Retailing" || PrimaryTrade == "Take Away Food Supplier" || PrimaryTrade == "Café" || PrimaryTrade == "Café (ex Deep Fat Frying)" || PrimaryTrade == "Fish And Chip Shop" || PrimaryTrade == "Pizza Delivery")
                                    || (SecondaryTrade == "Fast Food Retailing" || SecondaryTrade == "Take Away Food Supplier" || SecondaryTrade == "Café" || SecondaryTrade == "Café (ex Deep Fat Frying)" || SecondaryTrade == "Fish And Chip Shop" || SecondaryTrade == "Pizza Delivery")) // Flat roof
                                {
                                    if (service.RetrieveMultiple(new FetchExpression(fetch1)).Entities.Count == 0)
                                    {
                                        Entity ent = new Entity("lux_applicationendorsements");
                                        ent["lux_isdefault"] = true;
                                        ent["lux_endorsementhtml"] = Endorsement2.Attributes.Contains("lux_endorsementhtml") ? Endorsement2.Attributes["lux_endorsementhtml"] : "";
                                        ent["lux_endorsementnumber"] = Endorsement2.Attributes["new_endorsementnumber"];
                                        ent["lux_name"] = Endorsement2.Attributes["lux_name"];
                                        ent["lux_endorsementlibrary"] = new EntityReference("lux_endorsementlibrary", new Guid(Endorsement2.Attributes["lux_endorsementlibraryid"].ToString()));
                                        ent["lux_application"] = new EntityReference("lux_propertyownersapplications", appln.Id);
                                        service.Create(ent);
                                    }
                                }
                                //else
                                //{
                                //    if (service.RetrieveMultiple(new FetchExpression(fetch1)).Entities.Count > 0)
                                //    {
                                //        service.Delete("lux_applicationendorsements", service.RetrieveMultiple(new FetchExpression(fetch1)).Entities[0].Id);
                                //    }
                                //}
                            }

                            var Endorsement3 = service.RetrieveMultiple(new FetchExpression(endorsementFetch)).Entities.FirstOrDefault(x => x.Attributes["lux_name"].ToString() == "Roof Maintenance Condition");
                            if (Endorsement3 != null)
                            {
                                var fetch1 = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                                                          <entity name='lux_applicationendorsements'>
                                                            <attribute name='lux_applicationendorsementsid' />
                                                            <attribute name='lux_name' />
                                                            <attribute name='lux_endorsementnumber' />
                                                            <attribute name='createdon' />
                                                            <order attribute='lux_name' descending='false' />
                                                            <filter type='and'>
                                                              <condition attribute='statecode' operator='eq' value='0' />
                                                              <condition attribute='lux_endorsementlibrary' operator='eq' uiname='' uitype='lux_endorsementlibrary' value='{Endorsement3.Attributes["lux_endorsementlibraryid"]}' />
                                                              <condition attribute='lux_application' operator='eq' uiname='' uitype='lux_propertyownersapplications' value='{appln.Id}' />
                                                            </filter>
                                                          </entity>
                                                        </fetch>";

                                if (FlatRoof == true) // Flat roof
                                {
                                    if (service.RetrieveMultiple(new FetchExpression(fetch1)).Entities.Count == 0)
                                    {
                                        Entity ent = new Entity("lux_applicationendorsements");
                                        ent["lux_isdefault"] = true;
                                        ent["lux_endorsementhtml"] = Endorsement3.Attributes.Contains("lux_endorsementhtml") ? Endorsement3.Attributes["lux_endorsementhtml"] : "";
                                        ent["lux_endorsementnumber"] = Endorsement3.Attributes["new_endorsementnumber"];
                                        ent["lux_name"] = Endorsement3.Attributes["lux_name"];
                                        ent["lux_endorsementlibrary"] = new EntityReference("lux_endorsementlibrary", new Guid(Endorsement3.Attributes["lux_endorsementlibraryid"].ToString()));
                                        ent["lux_application"] = new EntityReference("lux_propertyownersapplications", appln.Id);
                                        service.Create(ent);
                                    }
                                }
                                //else
                                //{
                                //    if (service.RetrieveMultiple(new FetchExpression(fetch1)).Entities.Count > 0)
                                //    {
                                //        service.Delete("lux_applicationendorsements", service.RetrieveMultiple(new FetchExpression(fetch1)).Entities[0].Id);
                                //    }
                                //}
                            }

                            var Endorsement4 = service.RetrieveMultiple(new FetchExpression(endorsementFetch)).Entities.FirstOrDefault(x => x.Attributes["lux_name"].ToString() == "Heat Application Condition");
                            if (Endorsement4 != null)
                            {
                                var fetch1 = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                                                          <entity name='lux_applicationendorsements'>
                                                            <attribute name='lux_applicationendorsementsid' />
                                                            <attribute name='lux_name' />
                                                            <attribute name='lux_endorsementnumber' />
                                                            <attribute name='createdon' />
                                                            <order attribute='lux_name' descending='false' />
                                                            <filter type='and'>
                                                              <condition attribute='statecode' operator='eq' value='0' />
                                                              <condition attribute='lux_endorsementlibrary' operator='eq' uiname='' uitype='lux_endorsementlibrary' value='{Endorsement4.Attributes["lux_endorsementlibraryid"]}' />
                                                              <condition attribute='lux_application' operator='eq' uiname='' uitype='lux_propertyownersapplications' value='{appln.Id}' />
                                                            </filter>
                                                          </entity>
                                                        </fetch>";

                                if (AnyHeatWorkAway == true)
                                {
                                    if (service.RetrieveMultiple(new FetchExpression(fetch1)).Entities.Count == 0)
                                    {
                                        Entity ent = new Entity("lux_applicationendorsements");
                                        ent["lux_isdefault"] = true;
                                        ent["lux_endorsementhtml"] = Endorsement4.Attributes.Contains("lux_endorsementhtml") ? Endorsement4.Attributes["lux_endorsementhtml"] : "";
                                        ent["lux_endorsementnumber"] = Endorsement4.Attributes["new_endorsementnumber"];
                                        ent["lux_name"] = Endorsement4.Attributes["lux_name"];
                                        ent["lux_endorsementlibrary"] = new EntityReference("lux_endorsementlibrary", new Guid(Endorsement4.Attributes["lux_endorsementlibraryid"].ToString()));
                                        ent["lux_application"] = new EntityReference("lux_propertyownersapplications", appln.Id);
                                        service.Create(ent);
                                    }
                                }
                                //else
                                //{
                                //    if (service.RetrieveMultiple(new FetchExpression(fetch1)).Entities.Count > 0)
                                //    {
                                //        service.Delete("lux_applicationendorsements", service.RetrieveMultiple(new FetchExpression(fetch1)).Entities[0].Id);
                                //    }
                                //}
                            }

                            var FloodScore = item.Attributes.Contains("lux_floodscore") ? item.GetAttributeValue<int>("lux_floodscore") : 0;
                            var SubsidienceScore = item.Attributes.Contains("lux_subsidencescore") ? item.GetAttributeValue<int>("lux_subsidencescore") : 0;

                            var Endorsement5 = service.RetrieveMultiple(new FetchExpression(endorsementFetch)).Entities.FirstOrDefault(x => x.Attributes["lux_name"].ToString() == "Increased Flood Excess - £1,000");
                            if (Endorsement5 != null)
                            {
                                var fetch1 = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                                                          <entity name='lux_applicationendorsements'>
                                                            <attribute name='lux_applicationendorsementsid' />
                                                            <attribute name='lux_name' />
                                                            <attribute name='lux_endorsementnumber' />
                                                            <attribute name='createdon' />
                                                            <order attribute='lux_name' descending='false' />
                                                            <filter type='and'>
                                                              <condition attribute='statecode' operator='eq' value='0' />
                                                              <condition attribute='lux_endorsementlibrary' operator='eq' uiname='' uitype='lux_endorsementlibrary' value='{Endorsement5.Attributes["lux_endorsementlibraryid"]}' />
                                                              <condition attribute='lux_application' operator='eq' uiname='' uitype='lux_propertyownersapplications' value='{appln.Id}' />
                                                            </filter>
                                                          </entity>
                                                        </fetch>";
                                //if (model.IsLive == 0)
                                //{
                                if (FloodScore == 10 || Basement == true) //£1000 Flood Excess
                                {
                                    if (service.RetrieveMultiple(new FetchExpression(fetch1)).Entities.Count == 0)
                                    {
                                        Entity ent = new Entity("lux_applicationendorsements");
                                        ent["lux_isdefault"] = true;
                                        ent["lux_endorsementnumber"] = Endorsement5.Attributes["new_endorsementnumber"];
                                        ent["lux_endorsementhtml"] = Endorsement5.Attributes["lux_endorsementhtml"];
                                        ent["lux_name"] = Endorsement5.Attributes["lux_name"];
                                        ent["lux_endorsementlibrary"] = new EntityReference("lux_endorsementlibrary", new Guid(Endorsement5.Attributes["lux_endorsementlibraryid"].ToString()));
                                        ent["lux_application"] = new EntityReference("lux_propertyownersapplications", appln.Id);
                                        service.Create(ent);
                                    }
                                }
                                //else
                                //{
                                //    if (service.RetrieveMultiple(new FetchExpression(fetch1)).Entities.Count > 0)
                                //    {
                                //        service.Delete("lux_applicationendorsements", service.RetrieveMultiple(new FetchExpression(fetch1)).Entities[0].Id);
                                //    }
                                //}
                                //}
                                //else
                                //{
                                //    if (FloodScore == 10) //£1000 Flood Excess
                                //    {
                                //        if (service.RetrieveMultiple(new FetchExpression(fetch1)).Entities.Count == 0)
                                //        {
                                //            Entity ent = new Entity("lux_applicationendorsements");
                                //            ent["lux_isdefault"] = true;
                                //            ent["lux_endorsementnumber"] = Endorsement5.Attributes["new_endorsementnumber"];
                                //            ent["lux_endorsementhtml"] = Endorsement5.Attributes["lux_endorsementhtml"];
                                //            ent["lux_name"] = Endorsement5.Attributes["lux_name"];
                                //            ent["lux_endorsementlibrary"] = new EntityReference("lux_endorsementlibrary", new Guid(Endorsement5.Attributes["lux_endorsementlibraryid"].ToString()));
                                //            ent["lux_application"] = new EntityReference("lux_propertyownersapplications", appln.Id);
                                //            service.Create(ent);
                                //        }
                                //    }
                                //    //else
                                //    //{
                                //    //    if (service.RetrieveMultiple(new FetchExpression(fetch1)).Entities.Count > 0)
                                //    //    {
                                //    //        service.Delete("lux_applicationendorsements", service.RetrieveMultiple(new FetchExpression(fetch1)).Entities[0].Id);
                                //    //    }
                                //    //}
                                //}
                            }

                            var Endorsement6 = service.RetrieveMultiple(new FetchExpression(endorsementFetch)).Entities.FirstOrDefault(x => x.Attributes["lux_name"].ToString() == "Flood Exclusion");
                            if (Endorsement6 != null)
                            {
                                var fetch1 = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                                                          <entity name='lux_applicationendorsements'>
                                                            <attribute name='lux_applicationendorsementsid' />
                                                            <attribute name='lux_name' />
                                                            <attribute name='lux_endorsementnumber' />
                                                            <attribute name='createdon' />
                                                            <order attribute='lux_name' descending='false' />
                                                            <filter type='and'>
                                                              <condition attribute='statecode' operator='eq' value='0' />
                                                              <condition attribute='lux_endorsementlibrary' operator='eq' uiname='' uitype='lux_endorsementlibrary' value='{Endorsement6.Attributes["lux_endorsementlibraryid"]}' />
                                                              <condition attribute='lux_application' operator='eq' uiname='' uitype='lux_propertyownersapplications' value='{appln.Id}' />
                                                            </filter>
                                                          </entity>
                                                        </fetch>";
                                if (FloodScore >= 11) //Decline - Exclude Flood Cover
                                {
                                    if (service.RetrieveMultiple(new FetchExpression(fetch1)).Entities.Count == 0)
                                    {
                                        Entity ent = new Entity("lux_applicationendorsements");
                                        ent["lux_isdefault"] = true;
                                        ent["lux_endorsementnumber"] = Endorsement6.Attributes["new_endorsementnumber"];
                                        ent["lux_endorsementhtml"] = Endorsement6.Attributes["lux_endorsementhtml"];
                                        ent["lux_name"] = Endorsement6.Attributes["lux_name"];
                                        ent["lux_endorsementlibrary"] = new EntityReference("lux_endorsementlibrary", new Guid(Endorsement6.Attributes["lux_endorsementlibraryid"].ToString()));
                                        ent["lux_application"] = new EntityReference("lux_propertyownersapplications", appln.Id);
                                        service.Create(ent);
                                    }
                                }
                                //else
                                //{
                                //    if (service.RetrieveMultiple(new FetchExpression(fetch1)).Entities.Count > 0)
                                //    {
                                //        service.Delete("lux_applicationendorsements", service.RetrieveMultiple(new FetchExpression(fetch1)).Entities[0].Id);
                                //    }
                                //}
                            }
                            //if (model.IsLive == 1)
                            //{
                            //    var Endorsement7 = service.RetrieveMultiple(new FetchExpression(endorsementFetch)).Entities.FirstOrDefault(x => x.Attributes["lux_name"].ToString() == "Increased Subsidence Excess - £2,500");
                            //    if (Endorsement7 != null)
                            //    {
                            //        var fetch1 = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                            //                          <entity name='lux_applicationendorsements'>
                            //                            <attribute name='lux_applicationendorsementsid' />
                            //                            <attribute name='lux_name' />
                            //                            <attribute name='lux_endorsementnumber' />
                            //                            <attribute name='createdon' />
                            //                            <order attribute='lux_name' descending='false' />
                            //                            <filter type='and'>
                            //                              <condition attribute='statecode' operator='eq' value='0' />
                            //                              <condition attribute='lux_endorsementlibrary' operator='eq' uiname='' uitype='lux_endorsementlibrary' value='{Endorsement7.Attributes["lux_endorsementlibraryid"]}' />
                            //                              <condition attribute='lux_application' operator='eq' uiname='' uitype='lux_propertyownersapplications' value='{appln.Id}' />
                            //                            </filter>
                            //                          </entity>
                            //                        </fetch>";
                            //        if ((SubsidienceScore == 4 || SubsidienceScore == 5) && item.GetAttributeValue<bool>("lux_issubsidencecoverrequired") == true) //£750 Flood Excess
                            //        {
                            //            if (service.RetrieveMultiple(new FetchExpression(fetch1)).Entities.Count == 0)
                            //            {
                            //                Entity ent = new Entity("lux_applicationendorsements");
                            //                ent["lux_isdefault"] = true;
                            //                ent["lux_endorsementnumber"] = Endorsement7.Attributes["new_endorsementnumber"];
                            //                ent["lux_endorsementhtml"] = Endorsement7.Attributes["lux_endorsementhtml"];
                            //                ent["lux_name"] = Endorsement7.Attributes["lux_name"];
                            //                ent["lux_endorsementlibrary"] = new EntityReference("lux_endorsementlibrary", new Guid(Endorsement7.Attributes["lux_endorsementlibraryid"].ToString()));
                            //                ent["lux_application"] = new EntityReference("lux_propertyownersapplications", appln.Id);
                            //                service.Create(ent);
                            //            }
                            //        }
                            //        //else
                            //        //{
                            //        //    if (service.RetrieveMultiple(new FetchExpression(fetch1)).Entities.Count > 0)
                            //        //    {
                            //        //        service.Delete("lux_applicationendorsements", service.RetrieveMultiple(new FetchExpression(fetch1)).Entities[0].Id);
                            //        //    }
                            //        //}
                            //    }

                            //    var Endorsement8 = service.RetrieveMultiple(new FetchExpression(endorsementFetch)).Entities.FirstOrDefault(x => x.Attributes["lux_name"].ToString() == "Increased Subsidence Excess - £5,000");
                            //    if (Endorsement8 != null)
                            //    {
                            //        var fetch1 = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                            //                          <entity name='lux_applicationendorsements'>
                            //                            <attribute name='lux_applicationendorsementsid' />
                            //                            <attribute name='lux_name' />
                            //                            <attribute name='lux_endorsementnumber' />
                            //                            <attribute name='createdon' />
                            //                            <order attribute='lux_name' descending='false' />
                            //                            <filter type='and'>
                            //                              <condition attribute='statecode' operator='eq' value='0' />
                            //                              <condition attribute='lux_endorsementlibrary' operator='eq' uiname='' uitype='lux_endorsementlibrary' value='{Endorsement8.Attributes["lux_endorsementlibraryid"]}' />
                            //                              <condition attribute='lux_application' operator='eq' uiname='' uitype='lux_propertyownersapplications' value='{appln.Id}' />
                            //                            </filter>
                            //                          </entity>
                            //                        </fetch>";
                            //        if (SubsidienceScore == 6 && item.GetAttributeValue<bool>("lux_issubsidencecoverrequired") == true) //£750 Flood Excess
                            //        {
                            //            if (service.RetrieveMultiple(new FetchExpression(fetch1)).Entities.Count == 0)
                            //            {
                            //                Entity ent = new Entity("lux_applicationendorsements");
                            //                ent["lux_isdefault"] = true;
                            //                ent["lux_endorsementnumber"] = Endorsement8.Attributes["new_endorsementnumber"];
                            //                ent["lux_endorsementhtml"] = Endorsement8.Attributes["lux_endorsementhtml"];
                            //                ent["lux_name"] = Endorsement8.Attributes["lux_name"];
                            //                ent["lux_endorsementlibrary"] = new EntityReference("lux_endorsementlibrary", new Guid(Endorsement8.Attributes["lux_endorsementlibraryid"].ToString()));
                            //                ent["lux_application"] = new EntityReference("lux_propertyownersapplications", appln.Id);
                            //                service.Create(ent);
                            //            }
                            //        }
                            //        //else
                            //        //{
                            //        //    if (service.RetrieveMultiple(new FetchExpression(fetch1)).Entities.Count > 0)
                            //        //    {
                            //        //        service.Delete("lux_applicationendorsements", service.RetrieveMultiple(new FetchExpression(fetch1)).Entities[0].Id);
                            //        //    }
                            //        //}
                            //    }
                            //}
                            //else
                            //{
                            var Endorsement7 = service.RetrieveMultiple(new FetchExpression(endorsementFetch)).Entities.FirstOrDefault(x => x.Attributes["lux_name"].ToString() == "Increased Subsidence Excess - £2,500");
                            if (Endorsement7 != null)
                            {
                                var fetch1 = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                                                          <entity name='lux_applicationendorsements'>
                                                            <attribute name='lux_applicationendorsementsid' />
                                                            <attribute name='lux_name' />
                                                            <attribute name='lux_endorsementnumber' />
                                                            <attribute name='createdon' />
                                                            <order attribute='lux_name' descending='false' />
                                                            <filter type='and'>
                                                              <condition attribute='statecode' operator='eq' value='0' />
                                                              <condition attribute='lux_endorsementlibrary' operator='eq' uiname='' uitype='lux_endorsementlibrary' value='{Endorsement7.Attributes["lux_endorsementlibraryid"]}' />
                                                              <condition attribute='lux_application' operator='eq' uiname='' uitype='lux_propertyownersapplications' value='{appln.Id}' />
                                                            </filter>
                                                          </entity>
                                                        </fetch>";
                                if ((SubsidienceScore == 6) && item.GetAttributeValue<bool>("lux_issubsidencecoverrequired") == true) //£750 Flood Excess
                                {
                                    if (service.RetrieveMultiple(new FetchExpression(fetch1)).Entities.Count == 0)
                                    {
                                        Entity ent = new Entity("lux_applicationendorsements");
                                        ent["lux_isdefault"] = true;
                                        ent["lux_endorsementnumber"] = Endorsement7.Attributes["new_endorsementnumber"];
                                        ent["lux_endorsementhtml"] = Endorsement7.Attributes["lux_endorsementhtml"];
                                        ent["lux_name"] = Endorsement7.Attributes["lux_name"];
                                        ent["lux_endorsementlibrary"] = new EntityReference("lux_endorsementlibrary", new Guid(Endorsement7.Attributes["lux_endorsementlibraryid"].ToString()));
                                        ent["lux_application"] = new EntityReference("lux_propertyownersapplications", appln.Id);
                                        service.Create(ent);
                                    }
                                }
                            }
                            //}

                            var Endorsement9 = service.RetrieveMultiple(new FetchExpression(endorsementFetch)).Entities.FirstOrDefault(x => x.Attributes["lux_name"].ToString() == "Post Office Money Exclusion");
                            if (Endorsement9 != null)
                            {
                                var fetch1 = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                                                          <entity name='lux_applicationendorsements'>
                                                            <attribute name='lux_applicationendorsementsid' />
                                                            <attribute name='lux_name' />
                                                            <attribute name='lux_endorsementnumber' />
                                                            <attribute name='createdon' />
                                                            <order attribute='lux_name' descending='false' />
                                                            <filter type='and'>
                                                              <condition attribute='statecode' operator='eq' value='0' />
                                                              <condition attribute='lux_endorsementlibrary' operator='eq' uiname='' uitype='lux_endorsementlibrary' value='{Endorsement9.Attributes["lux_endorsementlibraryid"]}' />
                                                              <condition attribute='lux_application' operator='eq' uiname='' uitype='lux_propertyownersapplications' value='{appln.Id}' />
                                                            </filter>
                                                          </entity>
                                                        </fetch>";

                                if (PrimaryTrade.ToLower() == "post office" || PrimaryTrade.ToLower() == "post office - sub" || PrimaryTrade.ToLower() == "post office services" ||
                                    SecondaryTrade.ToLower() == "post office" || SecondaryTrade.ToLower() == "post office - sub" || SecondaryTrade.ToLower() == "post office services")
                                {
                                    if (service.RetrieveMultiple(new FetchExpression(fetch1)).Entities.Count == 0)
                                    {
                                        Entity ent = new Entity("lux_applicationendorsements");
                                        ent["lux_isdefault"] = true;
                                        ent["lux_endorsementnumber"] = Endorsement9.Attributes["new_endorsementnumber"];
                                        ent["lux_endorsementhtml"] = Endorsement9.Attributes["lux_endorsementhtml"];
                                        ent["lux_name"] = Endorsement9.Attributes["lux_name"];
                                        ent["lux_endorsementlibrary"] = new EntityReference("lux_endorsementlibrary", new Guid(Endorsement9.Attributes["lux_endorsementlibraryid"].ToString()));
                                        ent["lux_application"] = new EntityReference("lux_propertyownersapplications", appln.Id);
                                        service.Create(ent);
                                    }
                                }
                                //else
                                //{
                                //    if (service.RetrieveMultiple(new FetchExpression(fetch1)).Entities.Count > 0)
                                //    {
                                //        service.Delete("lux_applicationendorsements", service.RetrieveMultiple(new FetchExpression(fetch1)).Entities[0].Id);
                                //    }
                                //}
                            }

                            var Endorsement10 = service.RetrieveMultiple(new FetchExpression(endorsementFetch)).Entities.FirstOrDefault(x => x.Attributes["lux_name"].ToString() == "Subsidence Exclusion");
                            if (Endorsement10 != null)
                            {
                                var fetch1 = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                                                          <entity name='lux_applicationendorsements'>
                                                            <attribute name='lux_applicationendorsementsid' />
                                                            <attribute name='lux_name' />
                                                            <attribute name='lux_endorsementnumber' />
                                                            <attribute name='createdon' />
                                                            <order attribute='lux_name' descending='false' />
                                                            <filter type='and'>
                                                              <condition attribute='statecode' operator='eq' value='0' />
                                                              <condition attribute='lux_endorsementlibrary' operator='eq' uiname='' uitype='lux_endorsementlibrary' value='{Endorsement10.Attributes["lux_endorsementlibraryid"]}' />
                                                              <condition attribute='lux_application' operator='eq' uiname='' uitype='lux_propertyownersapplications' value='{appln.Id}' />
                                                            </filter>
                                                          </entity>
                                                        </fetch>";
                                if (SubsidienceScore == 7 && item.GetAttributeValue<bool>("lux_issubsidencecoverrequired") == true) //£750 Flood Excess
                                {
                                    if (service.RetrieveMultiple(new FetchExpression(fetch1)).Entities.Count == 0)
                                    {
                                        Entity ent = new Entity("lux_applicationendorsements");
                                        ent["lux_isdefault"] = true;
                                        ent["lux_endorsementnumber"] = Endorsement10.Attributes["new_endorsementnumber"];
                                        ent["lux_endorsementhtml"] = Endorsement10.Attributes["lux_endorsementhtml"];
                                        ent["lux_name"] = Endorsement10.Attributes["lux_name"];
                                        ent["lux_endorsementlibrary"] = new EntityReference("lux_endorsementlibrary", new Guid(Endorsement10.Attributes["lux_endorsementlibraryid"].ToString()));
                                        ent["lux_application"] = new EntityReference("lux_propertyownersapplications", appln.Id);
                                        service.Create(ent);
                                    }
                                }
                                //else
                                //{
                                //    if (service.RetrieveMultiple(new FetchExpression(fetch1)).Entities.Count > 0)
                                //    {
                                //        service.Delete("lux_applicationendorsements", service.RetrieveMultiple(new FetchExpression(fetch1)).Entities[0].Id);
                                //    }
                                //}
                            }

                            var Endorsement11 = service.RetrieveMultiple(new FetchExpression(endorsementFetch)).Entities.FirstOrDefault(x => x.Attributes["lux_name"].ToString() == "Increased Flood Excess - £500");
                            if (Endorsement11 != null)
                            {
                                var fetch1 = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                                                          <entity name='lux_applicationendorsements'>
                                                            <attribute name='lux_applicationendorsementsid' />
                                                            <attribute name='lux_name' />
                                                            <attribute name='lux_endorsementnumber' />
                                                            <attribute name='createdon' />
                                                            <order attribute='lux_name' descending='false' />
                                                            <filter type='and'>
                                                              <condition attribute='statecode' operator='eq' value='0' />
                                                              <condition attribute='lux_endorsementlibrary' operator='eq' uiname='' uitype='lux_endorsementlibrary' value='{Endorsement11.Attributes["lux_endorsementlibraryid"]}' />
                                                              <condition attribute='lux_application' operator='eq' uiname='' uitype='lux_propertyownersapplications' value='{appln.Id}' />
                                                            </filter>
                                                          </entity>
                                                        </fetch>";
                                if (FloodScore == 9) //£500 Flood Excess
                                {
                                    if (service.RetrieveMultiple(new FetchExpression(fetch1)).Entities.Count == 0)
                                    {
                                        Entity ent = new Entity("lux_applicationendorsements");
                                        ent["lux_isdefault"] = true;
                                        ent["lux_endorsementnumber"] = Endorsement11.Attributes["new_endorsementnumber"];
                                        ent["lux_endorsementhtml"] = Endorsement11.Attributes["lux_endorsementhtml"];
                                        ent["lux_name"] = Endorsement11.Attributes["lux_name"];
                                        ent["lux_endorsementlibrary"] = new EntityReference("lux_endorsementlibrary", new Guid(Endorsement11.Attributes["lux_endorsementlibraryid"].ToString()));
                                        ent["lux_application"] = new EntityReference("lux_propertyownersapplications", appln.Id);
                                        service.Create(ent);
                                    }
                                }
                                //else
                                //{
                                //    if (service.RetrieveMultiple(new FetchExpression(fetch1)).Entities.Count > 0)
                                //    {
                                //        service.Delete("lux_applicationendorsements", service.RetrieveMultiple(new FetchExpression(fetch1)).Entities[0].Id);
                                //    }
                                //}
                            }

                            var Endorsement12 = service.RetrieveMultiple(new FetchExpression(endorsementFetch)).Entities.FirstOrDefault(x => x.Attributes["lux_name"].ToString() == "Work Away Exclusion - Employers Liability");
                            if (Endorsement12 != null)
                            {
                                var fetch1 = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                                                          <entity name='lux_applicationendorsements'>
                                                            <attribute name='lux_applicationendorsementsid' />
                                                            <attribute name='lux_name' />
                                                            <attribute name='lux_endorsementnumber' />
                                                            <attribute name='createdon' />
                                                            <order attribute='lux_name' descending='false' />
                                                            <filter type='and'>
                                                              <condition attribute='statecode' operator='eq' value='0' />
                                                              <condition attribute='lux_endorsementlibrary' operator='eq' uiname='' uitype='lux_endorsementlibrary' value='{Endorsement12.Attributes["lux_endorsementlibraryid"]}' />
                                                              <condition attribute='lux_application' operator='eq' uiname='' uitype='lux_propertyownersapplications' value='{appln.Id}' />
                                                            </filter>
                                                          </entity>
                                                        </fetch>";

                                if (WorkAway == false)
                                {
                                    if (service.RetrieveMultiple(new FetchExpression(fetch1)).Entities.Count == 0)
                                    {
                                        Entity ent = new Entity("lux_applicationendorsements");
                                        ent["lux_isdefault"] = true;
                                        ent["lux_endorsementhtml"] = Endorsement12.Attributes.Contains("lux_endorsementhtml") ? Endorsement12.Attributes["lux_endorsementhtml"] : "";
                                        ent["lux_endorsementnumber"] = Endorsement12.Attributes["new_endorsementnumber"];
                                        ent["lux_name"] = Endorsement12.Attributes["lux_name"];
                                        ent["lux_endorsementlibrary"] = new EntityReference("lux_endorsementlibrary", new Guid(Endorsement12.Attributes["lux_endorsementlibraryid"].ToString()));
                                        ent["lux_application"] = new EntityReference("lux_propertyownersapplications", appln.Id);
                                        service.Create(ent);
                                    }
                                }
                                //else
                                //{
                                //    if (service.RetrieveMultiple(new FetchExpression(fetch1)).Entities.Count > 0)
                                //    {
                                //        service.Delete("lux_applicationendorsements", service.RetrieveMultiple(new FetchExpression(fetch1)).Entities[0].Id);
                                //    }
                                //}
                            }

                            var Endorsement13 = service.RetrieveMultiple(new FetchExpression(endorsementFetch)).Entities.FirstOrDefault(x => x.Attributes["lux_name"].ToString() == "Work Away Exclusion - Public and Products Liability");
                            if (Endorsement13 != null)
                            {
                                var fetch1 = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                                                          <entity name='lux_applicationendorsements'>
                                                            <attribute name='lux_applicationendorsementsid' />
                                                            <attribute name='lux_name' />
                                                            <attribute name='lux_endorsementnumber' />
                                                            <attribute name='createdon' />
                                                            <order attribute='lux_name' descending='false' />
                                                            <filter type='and'>
                                                              <condition attribute='statecode' operator='eq' value='0' />
                                                              <condition attribute='lux_endorsementlibrary' operator='eq' uiname='' uitype='lux_endorsementlibrary' value='{Endorsement13.Attributes["lux_endorsementlibraryid"]}' />
                                                              <condition attribute='lux_application' operator='eq' uiname='' uitype='lux_propertyownersapplications' value='{appln.Id}' />
                                                            </filter>
                                                          </entity>
                                                        </fetch>";

                                if (WorkAway == false)
                                {
                                    if (service.RetrieveMultiple(new FetchExpression(fetch1)).Entities.Count == 0)
                                    {
                                        Entity ent = new Entity("lux_applicationendorsements");
                                        ent["lux_isdefault"] = true;
                                        ent["lux_endorsementhtml"] = Endorsement13.Attributes.Contains("lux_endorsementhtml") ? Endorsement13.Attributes["lux_endorsementhtml"] : "";
                                        ent["lux_endorsementnumber"] = Endorsement13.Attributes["new_endorsementnumber"];
                                        ent["lux_name"] = Endorsement13.Attributes["lux_name"];
                                        ent["lux_endorsementlibrary"] = new EntityReference("lux_endorsementlibrary", new Guid(Endorsement13.Attributes["lux_endorsementlibraryid"].ToString()));
                                        ent["lux_application"] = new EntityReference("lux_propertyownersapplications", appln.Id);
                                        service.Create(ent);
                                    }
                                }
                                //else
                                //{
                                //    if (service.RetrieveMultiple(new FetchExpression(fetch1)).Entities.Count > 0)
                                //    {
                                //        service.Delete("lux_applicationendorsements", service.RetrieveMultiple(new FetchExpression(fetch1)).Entities[0].Id);
                                //    }
                                //}
                            }

                            var Endorsement14 = service.RetrieveMultiple(new FetchExpression(endorsementFetch)).Entities.FirstOrDefault(x => x.Attributes["lux_name"].ToString() == "Alarm Requirement: Audible Signalling");
                            if (Endorsement14 != null)
                            {
                                var fetch1 = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                                                          <entity name='lux_applicationendorsements'>
                                                            <attribute name='lux_applicationendorsementsid' />
                                                            <attribute name='lux_name' />
                                                            <attribute name='lux_endorsementnumber' />
                                                            <attribute name='createdon' />
                                                            <order attribute='lux_name' descending='false' />
                                                            <filter type='and'>
                                                              <condition attribute='statecode' operator='eq' value='0' />
                                                              <condition attribute='lux_endorsementlibrary' operator='eq' uiname='' uitype='lux_endorsementlibrary' value='{Endorsement14.Attributes["lux_endorsementlibraryid"]}' />
                                                              <condition attribute='lux_application' operator='eq' uiname='' uitype='lux_propertyownersapplications' value='{appln.Id}' />
                                                            </filter>
                                                          </entity>
                                                        </fetch>";

                                if (IntruderAlarm == true && TypeOFAlarm == 972970001)//audible only
                                {
                                    if (service.RetrieveMultiple(new FetchExpression(fetch1)).Entities.Count == 0)
                                    {
                                        Entity ent = new Entity("lux_applicationendorsements");
                                        ent["lux_isdefault"] = true;
                                        ent["lux_endorsementhtml"] = Endorsement14.Attributes.Contains("lux_endorsementhtml") ? Endorsement14.Attributes["lux_endorsementhtml"] : "";
                                        ent["lux_endorsementnumber"] = Endorsement14.Attributes["new_endorsementnumber"];
                                        ent["lux_name"] = Endorsement14.Attributes["lux_name"];
                                        ent["lux_endorsementlibrary"] = new EntityReference("lux_endorsementlibrary", new Guid(Endorsement14.Attributes["lux_endorsementlibraryid"].ToString()));
                                        ent["lux_application"] = new EntityReference("lux_propertyownersapplications", appln.Id);
                                        service.Create(ent);
                                    }
                                }
                                //else
                                //{
                                //    if (service.RetrieveMultiple(new FetchExpression(fetch1)).Entities.Count > 0)
                                //    {
                                //        service.Delete("lux_applicationendorsements", service.RetrieveMultiple(new FetchExpression(fetch1)).Entities[0].Id);
                                //    }
                                //}
                            }

                            var Endorsement15 = service.RetrieveMultiple(new FetchExpression(endorsementFetch)).Entities.FirstOrDefault(x => x.Attributes["lux_name"].ToString() == "Alarm Requirement: Non-Confirmed Signalling");
                            if (Endorsement15 != null)
                            {
                                var fetch1 = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                                                          <entity name='lux_applicationendorsements'>
                                                            <attribute name='lux_applicationendorsementsid' />
                                                            <attribute name='lux_name' />
                                                            <attribute name='lux_endorsementnumber' />
                                                            <attribute name='createdon' />
                                                            <order attribute='lux_name' descending='false' />
                                                            <filter type='and'>
                                                              <condition attribute='statecode' operator='eq' value='0' />
                                                              <condition attribute='lux_endorsementlibrary' operator='eq' uiname='' uitype='lux_endorsementlibrary' value='{Endorsement15.Attributes["lux_endorsementlibraryid"]}' />
                                                              <condition attribute='lux_application' operator='eq' uiname='' uitype='lux_propertyownersapplications' value='{appln.Id}' />
                                                            </filter>
                                                          </entity>
                                                        </fetch>";

                                if (IntruderAlarm == true && (TypeOFAlarm == 972970002 || TypeOFAlarm == 972970005))//redcare or digicom
                                {
                                    if (service.RetrieveMultiple(new FetchExpression(fetch1)).Entities.Count == 0)
                                    {
                                        Entity ent = new Entity("lux_applicationendorsements");
                                        ent["lux_isdefault"] = true;
                                        ent["lux_endorsementhtml"] = Endorsement15.Attributes.Contains("lux_endorsementhtml") ? Endorsement15.Attributes["lux_endorsementhtml"] : "";
                                        ent["lux_endorsementnumber"] = Endorsement15.Attributes["new_endorsementnumber"];
                                        ent["lux_name"] = Endorsement15.Attributes["lux_name"];
                                        ent["lux_endorsementlibrary"] = new EntityReference("lux_endorsementlibrary", new Guid(Endorsement15.Attributes["lux_endorsementlibraryid"].ToString()));
                                        ent["lux_application"] = new EntityReference("lux_propertyownersapplications", appln.Id);
                                        service.Create(ent);
                                    }
                                }
                                //else
                                //{
                                //    if (service.RetrieveMultiple(new FetchExpression(fetch1)).Entities.Count > 0)
                                //    {
                                //        service.Delete("lux_applicationendorsements", service.RetrieveMultiple(new FetchExpression(fetch1)).Entities[0].Id);
                                //    }
                                //}
                            }

                            var Endorsement16 = service.RetrieveMultiple(new FetchExpression(endorsementFetch)).Entities.FirstOrDefault(x => x.Attributes["lux_name"].ToString() == "Alarm Requirement: Confirmed Signalling");
                            if (Endorsement16 != null)
                            {
                                var fetch1 = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                                                          <entity name='lux_applicationendorsements'>
                                                            <attribute name='lux_applicationendorsementsid' />
                                                            <attribute name='lux_name' />
                                                            <attribute name='lux_endorsementnumber' />
                                                            <attribute name='createdon' />
                                                            <order attribute='lux_name' descending='false' />
                                                            <filter type='and'>
                                                              <condition attribute='statecode' operator='eq' value='0' />
                                                              <condition attribute='lux_endorsementlibrary' operator='eq' uiname='' uitype='lux_endorsementlibrary' value='{Endorsement16.Attributes["lux_endorsementlibraryid"]}' />
                                                              <condition attribute='lux_application' operator='eq' uiname='' uitype='lux_propertyownersapplications' value='{appln.Id}' />
                                                            </filter>
                                                          </entity>
                                                        </fetch>";

                                if (IntruderAlarm == true && (TypeOFAlarm == 972970003 || TypeOFAlarm == 972970004))//redcare gsm or dualcom
                                {
                                    if (service.RetrieveMultiple(new FetchExpression(fetch1)).Entities.Count == 0)
                                    {
                                        Entity ent = new Entity("lux_applicationendorsements");
                                        ent["lux_isdefault"] = true;
                                        ent["lux_endorsementhtml"] = Endorsement16.Attributes.Contains("lux_endorsementhtml") ? Endorsement16.Attributes["lux_endorsementhtml"] : "";
                                        ent["lux_endorsementnumber"] = Endorsement16.Attributes["new_endorsementnumber"];
                                        ent["lux_name"] = Endorsement16.Attributes["lux_name"];
                                        ent["lux_endorsementlibrary"] = new EntityReference("lux_endorsementlibrary", new Guid(Endorsement16.Attributes["lux_endorsementlibraryid"].ToString()));
                                        ent["lux_application"] = new EntityReference("lux_propertyownersapplications", appln.Id);
                                        service.Create(ent);
                                    }
                                }
                                //else
                                //{
                                //    if (service.RetrieveMultiple(new FetchExpression(fetch1)).Entities.Count > 0)
                                //    {
                                //        service.Delete("lux_applicationendorsements", service.RetrieveMultiple(new FetchExpression(fetch1)).Entities[0].Id);
                                //    }
                                //}
                            }

                            var Endorsement17 = service.RetrieveMultiple(new FetchExpression(endorsementFetch)).Entities.FirstOrDefault(x => x.Attributes["lux_name"].ToString() == "Treatment Liability Extension");
                            if (Endorsement17 != null)
                            {
                                var fetch1 = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                                                          <entity name='lux_applicationendorsements'>
                                                            <attribute name='lux_applicationendorsementsid' />
                                                            <attribute name='lux_name' />
                                                            <attribute name='lux_endorsementnumber' />
                                                            <attribute name='createdon' />
                                                            <order attribute='lux_name' descending='false' />
                                                            <filter type='and'>
                                                              <condition attribute='statecode' operator='eq' value='0' />
                                                              <condition attribute='lux_endorsementlibrary' operator='eq' uiname='' uitype='lux_endorsementlibrary' value='{Endorsement17.Attributes["lux_endorsementlibraryid"]}' />
                                                              <condition attribute='lux_application' operator='eq' uiname='' uitype='lux_propertyownersapplications' value='{appln.Id}' />
                                                            </filter>
                                                          </entity>
                                                        </fetch>";

                                if (PrimaryTrade.ToLower() == "hairdressing" || PrimaryTrade.ToLower() == "barber" || PrimaryTrade.ToLower() == "beauty salon" || PrimaryTrade.ToLower() == "beauty therapy" || PrimaryTrade.ToLower() == "beautician" ||
                                    SecondaryTrade.ToLower() == "hairdressing" || SecondaryTrade.ToLower() == "barber" || SecondaryTrade.ToLower() == "beauty salon" || SecondaryTrade.ToLower() == "beauty therapy" || SecondaryTrade.ToLower() == "beautician")
                                {
                                    if (service.RetrieveMultiple(new FetchExpression(fetch1)).Entities.Count == 0)
                                    {
                                        Entity ent = new Entity("lux_applicationendorsements");
                                        ent["lux_isdefault"] = true;
                                        ent["lux_endorsementnumber"] = Endorsement17.Attributes["new_endorsementnumber"];
                                        ent["lux_endorsementhtml"] = Endorsement17.Attributes.Contains("lux_endorsementhtml") ? Endorsement17.Attributes["lux_endorsementhtml"] : "";
                                        ent["lux_name"] = Endorsement17.Attributes["lux_name"];
                                        ent["lux_endorsementlibrary"] = new EntityReference("lux_endorsementlibrary", new Guid(Endorsement17.Attributes["lux_endorsementlibraryid"].ToString()));
                                        ent["lux_application"] = new EntityReference("lux_propertyownersapplications", appln.Id);
                                        service.Create(ent);
                                    }
                                }
                                //else
                                //{
                                //    if (service.RetrieveMultiple(new FetchExpression(fetch1)).Entities.Count > 0)
                                //    {
                                //        service.Delete("lux_applicationendorsements", service.RetrieveMultiple(new FetchExpression(fetch1)).Entities[0].Id);
                                //    }
                                //}
                            }

                            var Endorsement18 = service.RetrieveMultiple(new FetchExpression(endorsementFetch)).Entities.FirstOrDefault(x => x.Attributes["lux_name"].ToString() == "Gym and Fitness Clubs Condition");
                            if (Endorsement18 != null)
                            {
                                var fetch1 = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                                                          <entity name='lux_applicationendorsements'>
                                                            <attribute name='lux_applicationendorsementsid' />
                                                            <attribute name='lux_name' />
                                                            <attribute name='lux_endorsementnumber' />
                                                            <attribute name='createdon' />
                                                            <order attribute='lux_name' descending='false' />
                                                            <filter type='and'>
                                                              <condition attribute='statecode' operator='eq' value='0' />
                                                              <condition attribute='lux_endorsementlibrary' operator='eq' uiname='' uitype='lux_endorsementlibrary' value='{Endorsement18.Attributes["lux_endorsementlibraryid"]}' />
                                                              <condition attribute='lux_application' operator='eq' uiname='' uitype='lux_propertyownersapplications' value='{appln.Id}' />
                                                            </filter>
                                                          </entity>
                                                        </fetch>";

                                if (PrimaryTrade.ToLower() == "gymnasium" || PrimaryTrade.ToLower() == "health club" || PrimaryTrade.ToLower() == "leisure centre" || PrimaryTrade.ToLower() == "sports centre" ||
                                    SecondaryTrade.ToLower() == "gymnasium" || SecondaryTrade.ToLower() == "health club" || SecondaryTrade.ToLower() == "leisure centre" || SecondaryTrade.ToLower() == "sports centre")
                                {
                                    if (service.RetrieveMultiple(new FetchExpression(fetch1)).Entities.Count == 0)
                                    {
                                        Entity ent = new Entity("lux_applicationendorsements");
                                        ent["lux_isdefault"] = true;
                                        ent["lux_endorsementnumber"] = Endorsement18.Attributes["new_endorsementnumber"];
                                        ent["lux_endorsementhtml"] = Endorsement17.Attributes.Contains("lux_endorsementhtml") ? Endorsement17.Attributes["lux_endorsementhtml"] : "";
                                        ent["lux_name"] = Endorsement18.Attributes["lux_name"];
                                        ent["lux_endorsementlibrary"] = new EntityReference("lux_endorsementlibrary", new Guid(Endorsement18.Attributes["lux_endorsementlibraryid"].ToString()));
                                        ent["lux_application"] = new EntityReference("lux_propertyownersapplications", appln.Id);
                                        service.Create(ent);
                                    }
                                }
                                //else
                                //{
                                //    if (service.RetrieveMultiple(new FetchExpression(fetch1)).Entities.Count > 0)
                                //    {
                                //        service.Delete("lux_applicationendorsements", service.RetrieveMultiple(new FetchExpression(fetch1)).Entities[0].Id);
                                //    }
                                //}
                            }

                            var Endorsement19 = service.RetrieveMultiple(new FetchExpression(endorsementFetch)).Entities.FirstOrDefault(x => x.Attributes["lux_name"].ToString() == "Products Exclusion");
                            if (Endorsement19 != null)
                            {
                                var fetch1 = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                                                          <entity name='lux_applicationendorsements'>
                                                            <attribute name='lux_applicationendorsementsid' />
                                                            <attribute name='lux_name' />
                                                            <attribute name='lux_endorsementnumber' />
                                                            <attribute name='createdon' />
                                                            <order attribute='lux_name' descending='false' />
                                                            <filter type='and'>
                                                              <condition attribute='statecode' operator='eq' value='0' />
                                                              <condition attribute='lux_endorsementlibrary' operator='eq' uiname='' uitype='lux_endorsementlibrary' value='{Endorsement19.Attributes["lux_endorsementlibraryid"]}' />
                                                              <condition attribute='lux_application' operator='eq' uiname='' uitype='lux_propertyownersapplications' value='{appln.Id}' />
                                                            </filter>
                                                          </entity>
                                                        </fetch>";

                                if (ProdusHazardGrade >= 6)
                                {
                                    if (service.RetrieveMultiple(new FetchExpression(fetch1)).Entities.Count == 0)
                                    {
                                        Entity ent = new Entity("lux_applicationendorsements");
                                        ent["lux_isdefault"] = true;
                                        ent["lux_endorsementnumber"] = Endorsement19.Attributes["new_endorsementnumber"];
                                        ent["lux_endorsementhtml"] = Endorsement19.Attributes["lux_endorsementhtml"];
                                        ent["lux_name"] = Endorsement19.Attributes["lux_name"];
                                        ent["lux_endorsementlibrary"] = new EntityReference("lux_endorsementlibrary", new Guid(Endorsement19.Attributes["lux_endorsementlibraryid"].ToString()));
                                        ent["lux_application"] = new EntityReference("lux_propertyownersapplications", appln.Id);
                                        service.Create(ent);
                                    }
                                }
                                //else
                                //{
                                //    if (service.RetrieveMultiple(new FetchExpression(fetch1)).Entities.Count > 0)
                                //    {
                                //        service.Delete("lux_applicationendorsements", service.RetrieveMultiple(new FetchExpression(fetch1)).Entities[0].Id);
                                //    }
                                //}
                            }

                            var Endorsement20 = service.RetrieveMultiple(new FetchExpression(endorsementFetch)).Entities.FirstOrDefault(x => x.Attributes["lux_name"].ToString() == "Post Office Money Exclusion - Business Interruption");
                            if (Endorsement20 != null)
                            {
                                var fetch1 = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                                                          <entity name='lux_applicationendorsements'>
                                                            <attribute name='lux_applicationendorsementsid' />
                                                            <attribute name='lux_name' />
                                                            <attribute name='lux_endorsementnumber' />
                                                            <attribute name='createdon' />
                                                            <order attribute='lux_name' descending='false' />
                                                            <filter type='and'>
                                                              <condition attribute='statecode' operator='eq' value='0' />
                                                              <condition attribute='lux_endorsementlibrary' operator='eq' uiname='' uitype='lux_endorsementlibrary' value='{Endorsement20.Attributes["lux_endorsementlibraryid"]}' />
                                                              <condition attribute='lux_application' operator='eq' uiname='' uitype='lux_propertyownersapplications' value='{appln.Id}' />
                                                            </filter>
                                                          </entity>
                                                        </fetch>";

                                if (PrimaryTrade.ToLower() == "post office" || PrimaryTrade.ToLower() == "post office - sub" || PrimaryTrade.ToLower() == "post office services"
                                    || SecondaryTrade.ToLower() == "post office" || SecondaryTrade.ToLower() == "post office - sub" || SecondaryTrade.ToLower() == "post office services")
                                {
                                    if (service.RetrieveMultiple(new FetchExpression(fetch1)).Entities.Count == 0)
                                    {
                                        Entity ent = new Entity("lux_applicationendorsements");
                                        ent["lux_isdefault"] = true;
                                        ent["lux_endorsementnumber"] = Endorsement20.Attributes["new_endorsementnumber"];
                                        ent["lux_endorsementhtml"] = Endorsement20.Attributes["lux_endorsementhtml"];
                                        ent["lux_name"] = Endorsement20.Attributes["lux_name"];
                                        ent["lux_endorsementlibrary"] = new EntityReference("lux_endorsementlibrary", new Guid(Endorsement20.Attributes["lux_endorsementlibraryid"].ToString()));
                                        ent["lux_application"] = new EntityReference("lux_propertyownersapplications", appln.Id);
                                        service.Create(ent);
                                    }
                                }
                                //else
                                //{
                                //    if (service.RetrieveMultiple(new FetchExpression(fetch1)).Entities.Count > 0)
                                //    {
                                //        service.Delete("lux_applicationendorsements", service.RetrieveMultiple(new FetchExpression(fetch1)).Entities[0].Id);
                                //    }
                                //}
                            }

                            var Endorsement21 = service.RetrieveMultiple(new FetchExpression(endorsementFetch)).Entities.FirstOrDefault(x => x.Attributes["lux_name"].ToString() == "Unattended Machinery Condition");
                            if (Endorsement21 != null)
                            {
                                var fetch1 = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                                                          <entity name='lux_applicationendorsements'>
                                                            <attribute name='lux_applicationendorsementsid' />
                                                            <attribute name='lux_name' />
                                                            <attribute name='lux_endorsementnumber' />
                                                            <attribute name='createdon' />
                                                            <order attribute='lux_name' descending='false' />
                                                            <filter type='and'>
                                                              <condition attribute='statecode' operator='eq' value='0' />
                                                              <condition attribute='lux_endorsementlibrary' operator='eq' uiname='' uitype='lux_endorsementlibrary' value='{Endorsement21.Attributes["lux_endorsementlibraryid"]}' />
                                                              <condition attribute='lux_application' operator='eq' uiname='' uitype='lux_propertyownersapplications' value='{appln.Id}' />
                                                            </filter>
                                                          </entity>
                                                        </fetch>";

                                if (PrimaryTrade.ToLower() == "bakery" || PrimaryTrade.ToLower() == "baker" || PrimaryTrade.ToLower() == "cake making and decorating" ||
                                    SecondaryTrade.ToLower() == "bakery" || SecondaryTrade.ToLower() == "baker" || SecondaryTrade.ToLower() == "cake making and decorating")
                                {
                                    if (service.RetrieveMultiple(new FetchExpression(fetch1)).Entities.Count == 0)
                                    {
                                        Entity ent = new Entity("lux_applicationendorsements");
                                        ent["lux_application"] = new EntityReference("lux_propertyownersapplications", appln.Id);
                                        ent["lux_isdefault"] = true;
                                        ent["lux_endorsementnumber"] = Endorsement21.Attributes["new_endorsementnumber"];
                                        ent["lux_endorsementhtml"] = Endorsement21.Attributes["lux_endorsementhtml"];
                                        ent["lux_name"] = Endorsement21.Attributes["lux_name"];
                                        ent["lux_endorsementlibrary"] = new EntityReference("lux_endorsementlibrary", new Guid(Endorsement21.Attributes["lux_endorsementlibraryid"].ToString()));
                                        service.Create(ent);
                                    }
                                }
                                //else
                                //{
                                //    if (service.RetrieveMultiple(new FetchExpression(fetch1)).Entities.Count > 0)
                                //    {
                                //        service.Delete("lux_applicationendorsements", service.RetrieveMultiple(new FetchExpression(fetch1)).Entities[0].Id);
                                //    }
                                //}
                            }

                            var Endorsement22 = service.RetrieveMultiple(new FetchExpression(endorsementFetch)).Entities.FirstOrDefault(x => x.Attributes["lux_name"].ToString() == "Waste Condition - Bakers");
                            if (Endorsement22 != null)
                            {
                                var fetch1 = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                                                          <entity name='lux_applicationendorsements'>
                                                            <attribute name='lux_applicationendorsementsid' />
                                                            <attribute name='lux_name' />
                                                            <attribute name='lux_endorsementnumber' />
                                                            <attribute name='createdon' />
                                                            <order attribute='lux_name' descending='false' />
                                                            <filter type='and'>
                                                              <condition attribute='statecode' operator='eq' value='0' />
                                                              <condition attribute='lux_endorsementlibrary' operator='eq' uiname='' uitype='lux_endorsementlibrary' value='{Endorsement22.Attributes["lux_endorsementlibraryid"]}' />
                                                              <condition attribute='lux_application' operator='eq' uiname='' uitype='lux_propertyownersapplications' value='{appln.Id}' />
                                                            </filter>
                                                          </entity>
                                                        </fetch>";

                                if (PrimaryTrade.ToLower() == "bakery" || PrimaryTrade.ToLower() == "baker" || PrimaryTrade.ToLower() == "cake making and decorating" ||
                                    SecondaryTrade.ToLower() == "bakery" || SecondaryTrade.ToLower() == "baker" || SecondaryTrade.ToLower() == "cake making and decorating")
                                {
                                    if (service.RetrieveMultiple(new FetchExpression(fetch1)).Entities.Count == 0)
                                    {
                                        Entity ent = new Entity("lux_applicationendorsements");
                                        ent["lux_application"] = new EntityReference("lux_propertyownersapplications", appln.Id);
                                        ent["lux_isdefault"] = true;
                                        ent["lux_endorsementnumber"] = Endorsement22.Attributes["new_endorsementnumber"];
                                        ent["lux_endorsementhtml"] = Endorsement22.Attributes["lux_endorsementhtml"];
                                        ent["lux_name"] = Endorsement22.Attributes["lux_name"];
                                        ent["lux_endorsementlibrary"] = new EntityReference("lux_endorsementlibrary", new Guid(Endorsement22.Attributes["lux_endorsementlibraryid"].ToString()));
                                        service.Create(ent);
                                    }
                                }
                                //else
                                //{
                                //    if (service.RetrieveMultiple(new FetchExpression(fetch1)).Entities.Count > 0)
                                //    {
                                //        service.Delete("lux_applicationendorsements", service.RetrieveMultiple(new FetchExpression(fetch1)).Entities[0].Id);
                                //    }
                                //}
                            }

                            //if (model.IsLive == 0)
                            //{
                            var Endorsement23 = service.RetrieveMultiple(new FetchExpression(endorsementFetch)).Entities.FirstOrDefault(x => x.Attributes["lux_name"].ToString() == "Flat Roof - Increased Excess");
                            if (Endorsement23 != null)
                            {
                                var percent = item.Attributes.Contains("lux_flatroofpercentage") ? item.Attributes["lux_flatroofpercentage"].ToString().Replace("%", "") : "0";
                                percent = Regex.Match(percent, @"\d+").Value;

                                var fetch1 = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                                                              <entity name='lux_applicationendorsements'>
                                                                <attribute name='lux_applicationendorsementsid' />
                                                                <attribute name='lux_name' />
                                                                <attribute name='lux_endorsementnumber' />
                                                                <attribute name='createdon' />
                                                                <order attribute='lux_name' descending='false' />
                                                                <filter type='and'>
                                                                  <condition attribute='statecode' operator='eq' value='0' />
                                                                  <condition attribute='lux_endorsementlibrary' operator='eq' uiname='Wood Burner Condition' uitype='lux_endorsementlibrary' value='{Endorsement23.Attributes["lux_endorsementlibraryid"]}' />
                                                                  <condition attribute='lux_application' operator='eq' uiname='' uitype='lux_propertyownersapplications' value='{appln.Id}' />
                                                                </filter>
                                                              </entity>
                                                            </fetch>";

                                if (FlatRoof == true && Convert.ToDecimal(percent) > 50) // Flat roof
                                {
                                    if (service.RetrieveMultiple(new FetchExpression(fetch1)).Entities.Count == 0)
                                    {
                                        Entity ent = new Entity("lux_applicationendorsements");
                                        ent["lux_isdefault"] = true;
                                        ent["lux_endorsementhtml"] = Endorsement23.Attributes["lux_endorsementhtml"];
                                        ent["lux_endorsementnumber"] = Endorsement23.Attributes["new_endorsementnumber"];
                                        ent["lux_name"] = Endorsement23.Attributes["lux_name"];
                                        ent["lux_endorsementlibrary"] = new EntityReference("lux_endorsementlibrary", new Guid(Endorsement23.Attributes["lux_endorsementlibraryid"].ToString()));
                                        ent["lux_application"] = new EntityReference("lux_propertyownersapplications", appln.Id);
                                        service.Create(ent);
                                    }
                                }
                                //else
                                //{
                                //    if (service.RetrieveMultiple(new FetchExpression(fetch1)).Entities.Count > 0)
                                //    {
                                //        service.Delete("lux_applicationendorsements", service.RetrieveMultiple(new FetchExpression(fetch1)).Entities[0].Id);
                                //    }
                                //}
                            }

                            var Endorsement24 = service.RetrieveMultiple(new FetchExpression(endorsementFetch)).Entities.FirstOrDefault(x => x.Attributes["lux_name"].ToString() == "Electrical Inspection and Testing Programme");
                            if (Endorsement24 != null)
                            {
                                var fetch1 = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                                                              <entity name='lux_applicationendorsements'>
                                                                <attribute name='lux_applicationendorsementsid' />
                                                                <attribute name='lux_name' />
                                                                <attribute name='lux_endorsementnumber' />
                                                                <attribute name='createdon' />
                                                                <order attribute='lux_name' descending='false' />
                                                                <filter type='and'>
                                                                  <condition attribute='statecode' operator='eq' value='0' />
                                                                  <condition attribute='lux_endorsementlibrary' operator='eq' uiname='' uitype='lux_endorsementlibrary' value='{Endorsement24.Attributes["lux_endorsementlibraryid"]}' />
                                                                  <condition attribute='lux_application' operator='eq' uiname='' uitype='lux_propertyownersapplications' value='{appln.Id}' />
                                                                </filter>
                                                              </entity>
                                                            </fetch>";

                                if (service.RetrieveMultiple(new FetchExpression(fetch1)).Entities.Count == 0)
                                {
                                    Entity ent = new Entity("lux_applicationendorsements");
                                    ent["lux_application"] = new EntityReference("lux_propertyownersapplications", appln.Id);
                                    ent["lux_isdefault"] = true;
                                    ent["lux_endorsementnumber"] = Endorsement24.Attributes["new_endorsementnumber"];
                                    ent["lux_endorsementhtml"] = Endorsement24.Attributes["lux_endorsementhtml"];
                                    ent["lux_name"] = Endorsement24.Attributes["lux_name"];
                                    ent["lux_endorsementlibrary"] = new EntityReference("lux_endorsementlibrary", new Guid(Endorsement24.Attributes["lux_endorsementlibraryid"].ToString()));
                                    service.Create(ent);
                                }
                            }

                            var Endorsement25 = service.RetrieveMultiple(new FetchExpression(endorsementFetch)).Entities.FirstOrDefault(x => x.Attributes["lux_name"].ToString() == "No Smoking Condition");
                            if (Endorsement25 != null)
                            {
                                var fetch1 = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                                                              <entity name='lux_applicationendorsements'>
                                                                <attribute name='lux_applicationendorsementsid' />
                                                                <attribute name='lux_name' />
                                                                <attribute name='lux_endorsementnumber' />
                                                                <attribute name='createdon' />
                                                                <order attribute='lux_name' descending='false' />
                                                                <filter type='and'>
                                                                  <condition attribute='statecode' operator='eq' value='0' />
                                                                  <condition attribute='lux_endorsementlibrary' operator='eq' uiname='' uitype='lux_endorsementlibrary' value='{Endorsement25.Attributes["lux_endorsementlibraryid"]}' />
                                                                  <condition attribute='lux_application' operator='eq' uiname='' uitype='lux_propertyownersapplications' value='{appln.Id}' />
                                                                </filter>
                                                              </entity>
                                                            </fetch>";

                                if (service.RetrieveMultiple(new FetchExpression(fetch1)).Entities.Count == 0)
                                {
                                    Entity ent = new Entity("lux_applicationendorsements");
                                    ent["lux_application"] = new EntityReference("lux_propertyownersapplications", appln.Id);
                                    ent["lux_isdefault"] = true;
                                    ent["lux_endorsementnumber"] = Endorsement25.Attributes["new_endorsementnumber"];
                                    ent["lux_endorsementhtml"] = Endorsement25.Attributes["lux_endorsementhtml"];
                                    ent["lux_name"] = Endorsement25.Attributes["lux_name"];
                                    ent["lux_endorsementlibrary"] = new EntityReference("lux_endorsementlibrary", new Guid(Endorsement25.Attributes["lux_endorsementlibraryid"].ToString()));
                                    service.Create(ent);
                                }
                            }
                            //}
                        }
                    }
                }
            }
            else if (productName == "Commercial Combined")
            {
                var fetch = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                                      <entity name='lux_commercialcombinedapplication'>
                                        <attribute name='lux_commercialcombinedapplicationid' />
                                        <attribute name='lux_name' />
                                        <attribute name='createdon' />
                                        <attribute name='lux_rentreceivable' />
                                        <attribute name='lux_icow' />
                                        <attribute name='lux_typeofcover' />
                                        <attribute name='lux_bookdebts' />
                                        <attribute name='lux_amount' />
                                        <attribute name='lux_issubsidencecoverrequired' />
                                        <attribute name='lux_doesthepremiseshaveaflatroof' />
                                        <attribute name='lux_flatroofpercentage' />
                                        <attribute name='lux_doesthepremiseshaveabasement' />
                                        <attribute name='lux_isanymanufacturingprocessoperatingunatten' />
                                        <attribute name='lux_isthereanatm' />
                                        <attribute name='lux_isthereanintruderalarminstalledandinworki' />                                
                                        <attribute name='lux_whattypeofalarmisinstalled' />
                                        <attribute name='lux_isownerresponsibleforfillingandemptyingth' />
                                        <attribute name='lux_isthereanydeepfatfrying' />
                                        <attribute name='lux_additionalincreasedcostofworking' />
                                        <attribute name='lux_floodscore' />
                                        <attribute name='lux_subsidencescore' />
                                        <attribute name='lux_crimescore' />
                                        <attribute name='lux_securityrating' />
                                        <order attribute='lux_name' descending='false' />
                                        <filter type='and'>
                                          <condition attribute='lux_propertyownersapplications' operator='eq' uiname='' uitype='lux_propertyownersapplications' value='{appln.Id}' />
                                        </filter>
                                        <link-entity name='lux_propertyownersapplications' from='lux_propertyownersapplicationsid' to='lux_propertyownersapplications' visible='false' link-type='outer' alias='appln'>
                                              <attribute name='lux_anyheatworkawayundertaken' />
                                              <attribute name='lux_maintradeforthispremises' />
                                              <attribute name='lux_secondarytradeofthebusiness' />
                                              <attribute name='createdon' />
                                              <attribute name='lux_isanyworkawaycarriedoutotherthanforcollec' />                                  
                                              <attribute name='lux_doyouundertakeanyworkabove10minheight' />
                                              <attribute name='lux_doyouundertakeanyworkatdepth' />
                                        </link-entity>
                                      </entity>
                                    </fetch>";

                if (service.RetrieveMultiple(new FetchExpression(fetch)).Entities.Count > 0)
                {
                    var endorsementFetch = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                                                          <entity name='lux_endorsementlibrary'>
                                                            <attribute name='lux_name' />
                                                            <attribute name='new_product' />
                                                            <attribute name='lux_insurer' />
                                                            <attribute name='lux_endorsementdescription' />
                                                            <attribute name='lux_endorsementhtml' />
                                                            <attribute name='new_endorsementnumber' />
                                                            <attribute name='lux_endorsementlibraryid' />
                                                            <order attribute='lux_name' descending='false' />
                                                            <filter type='and'>
                                                              <condition attribute='statecode' operator='eq' value='0' />
                                                              <condition attribute='lux_name' operator='not-null' />
                                                              <filter type='or'>
                                                                <condition attribute='new_product' operator='eq' uiname='' uitype='product' value='{"8008008f-aaa1-eb11-b1ac-00224840d300"}' />
                                                                <condition attribute='new_product' operator='null' />
                                                              </filter>
                                                            </filter>
                                                          </entity>
                                                        </fetch>";

                    foreach (var item in service.RetrieveMultiple(new FetchExpression(fetch)).Entities)
                    {
                        var FlatRoof = item.Attributes.Contains("lux_doesthepremiseshaveaflatroof") ? item.GetAttributeValue<bool>("lux_doesthepremiseshaveaflatroof") : false;
                        var ATM = item.Attributes.Contains("lux_isthereanatm") ? item.GetAttributeValue<bool>("lux_isthereanatm") : false;
                        var OwnerResponsible = item.Attributes.Contains("lux_isownerresponsibleforfillingandemptyingth") ? item.GetAttributeValue<bool>("lux_isownerresponsibleforfillingandemptyingth") : false;
                        var AnyDFF = item.Attributes.Contains("lux_isthereanydeepfatfrying") ? item.GetAttributeValue<bool>("lux_isthereanydeepfatfrying") : false;
                        var AnyHeatWorkAway = item.Attributes.Contains("appln.lux_anyheatworkawayundertaken") ? ((bool)(item.GetAttributeValue<AliasedValue>("appln.lux_anyheatworkawayundertaken").Value)) : false;
                        var WorkAway = item.Attributes.Contains("appln.lux_isanyworkawaycarriedoutotherthanforcollec") ? ((bool)(item.GetAttributeValue<AliasedValue>("appln.lux_isanyworkawaycarriedoutotherthanforcollec").Value)) : false;
                        var IntruderAlarm = item.Attributes.Contains("lux_isthereanintruderalarminstalledandinworki") ? item.GetAttributeValue<bool>("lux_isthereanintruderalarminstalledandinworki") : false;
                        var TypeOFAlarm = item.Attributes.Contains("lux_whattypeofalarmisinstalled") ? item.GetAttributeValue<OptionSetValue>("lux_whattypeofalarmisinstalled").Value : 0;
                        var PrimaryTrade = item.Attributes.Contains("appln.lux_maintradeforthispremises") ? item.FormattedValues["appln.lux_maintradeforthispremises"].ToString() : "";
                        var SecondaryTrade = item.Attributes.Contains("appln.lux_secondarytradeofthebusiness") ? item.FormattedValues["appln.lux_secondarytradeofthebusiness"].ToString() : "";
                        var Basement = item.Attributes.Contains("lux_doesthepremiseshaveabasement") ? item.GetAttributeValue<bool>("lux_doesthepremiseshaveabasement") : false;

                        if (service.RetrieveMultiple(new FetchExpression(endorsementFetch)).Entities.Count > 0)
                        {
                            var Endorsement1 = new Entity();
                            if (OwnerResponsible == true)
                            {
                                Endorsement1 = service.RetrieveMultiple(new FetchExpression(endorsementFetch)).Entities.FirstOrDefault(x => x.Attributes["lux_name"].ToString() == "Automated Teller Machine (ATM) - Self Fill");
                            }
                            else
                            {
                                Endorsement1 = service.RetrieveMultiple(new FetchExpression(endorsementFetch)).Entities.FirstOrDefault(x => x.Attributes["lux_name"].ToString() == "Automated Teller Machine (ATM) - Third Party Provided & Maintained Machines");
                            }

                            if (Endorsement1 != null)
                            {
                                var fetch1 = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                                                          <entity name='lux_applicationendorsements'>
                                                            <attribute name='lux_applicationendorsementsid' />
                                                            <attribute name='lux_name' />
                                                            <attribute name='lux_endorsementnumber' />
                                                            <attribute name='createdon' />
                                                            <order attribute='lux_name' descending='false' />
                                                            <filter type='and'>
                                                              <condition attribute='statecode' operator='eq' value='0' />
                                                              <condition attribute='lux_endorsementlibrary' operator='eq' uiname='' uitype='lux_endorsementlibrary' value='{Endorsement1.Attributes["lux_endorsementlibraryid"]}' />
                                                              <condition attribute='lux_application' operator='eq' uiname='' uitype='lux_propertyownersapplications' value='{appln.Id}' />
                                                            </filter>
                                                          </entity>
                                                        </fetch>";

                                if (ATM == true) // Flat roof
                                {
                                    if (service.RetrieveMultiple(new FetchExpression(fetch1)).Entities.Count == 0)
                                    {
                                        Entity ent = new Entity("lux_applicationendorsements");
                                        ent["lux_isdefault"] = true;
                                        ent["lux_endorsementhtml"] = Endorsement1.Attributes.Contains("lux_endorsementhtml") ? Endorsement1.Attributes["lux_endorsementhtml"] : "";
                                        ent["lux_endorsementnumber"] = Endorsement1.Attributes["new_endorsementnumber"];
                                        ent["lux_name"] = Endorsement1.Attributes["lux_name"];
                                        ent["lux_endorsementlibrary"] = new EntityReference("lux_endorsementlibrary", new Guid(Endorsement1.Attributes["lux_endorsementlibraryid"].ToString()));
                                        ent["lux_application"] = new EntityReference("lux_propertyownersapplications", appln.Id);
                                        service.Create(ent);
                                    }
                                }
                                //else
                                //{
                                //    if (service.RetrieveMultiple(new FetchExpression(fetch1)).Entities.Count > 0)
                                //    {
                                //        service.Delete("lux_applicationendorsements", service.RetrieveMultiple(new FetchExpression(fetch1)).Entities[0].Id);
                                //    }
                                //}
                            }

                            var Endorsement2 = service.RetrieveMultiple(new FetchExpression(endorsementFetch)).Entities.FirstOrDefault(x => x.Attributes["lux_name"].ToString() == "Kitchen Precautions");
                            if (Endorsement2 != null)
                            {
                                var fetch1 = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                                                          <entity name='lux_applicationendorsements'>
                                                            <attribute name='lux_applicationendorsementsid' />
                                                            <attribute name='lux_name' />
                                                            <attribute name='lux_endorsementnumber' />
                                                            <attribute name='createdon' />
                                                            <order attribute='lux_name' descending='false' />
                                                            <filter type='and'>
                                                              <condition attribute='statecode' operator='eq' value='0' />
                                                              <condition attribute='lux_endorsementlibrary' operator='eq' uiname='' uitype='lux_endorsementlibrary' value='{Endorsement2.Attributes["lux_endorsementlibraryid"]}' />
                                                              <condition attribute='lux_application' operator='eq' uiname='' uitype='lux_propertyownersapplications' value='{appln.Id}' />
                                                            </filter>
                                                          </entity>
                                                        </fetch>";

                                if (AnyDFF == true) // Flat roof
                                {
                                    if (service.RetrieveMultiple(new FetchExpression(fetch1)).Entities.Count == 0)
                                    {
                                        Entity ent = new Entity("lux_applicationendorsements");
                                        ent["lux_isdefault"] = true;
                                        ent["lux_endorsementhtml"] = Endorsement2.Attributes.Contains("lux_endorsementhtml") ? Endorsement2.Attributes["lux_endorsementhtml"] : "";
                                        ent["lux_endorsementnumber"] = Endorsement2.Attributes["new_endorsementnumber"];
                                        ent["lux_name"] = Endorsement2.Attributes["lux_name"];
                                        ent["lux_endorsementlibrary"] = new EntityReference("lux_endorsementlibrary", new Guid(Endorsement2.Attributes["lux_endorsementlibraryid"].ToString()));
                                        ent["lux_application"] = new EntityReference("lux_propertyownersapplications", appln.Id);
                                        service.Create(ent);
                                    }
                                }
                                //else
                                //{
                                //    if (service.RetrieveMultiple(new FetchExpression(fetch1)).Entities.Count > 0)
                                //    {
                                //        service.Delete("lux_applicationendorsements", service.RetrieveMultiple(new FetchExpression(fetch1)).Entities[0].Id);
                                //    }
                                //}
                            }

                            var Endorsement3 = service.RetrieveMultiple(new FetchExpression(endorsementFetch)).Entities.FirstOrDefault(x => x.Attributes["lux_name"].ToString() == "Roof Maintenance Condition");
                            if (Endorsement3 != null)
                            {
                                var fetch1 = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                                                          <entity name='lux_applicationendorsements'>
                                                            <attribute name='lux_applicationendorsementsid' />
                                                            <attribute name='lux_name' />
                                                            <attribute name='lux_endorsementnumber' />
                                                            <attribute name='createdon' />
                                                            <order attribute='lux_name' descending='false' />
                                                            <filter type='and'>
                                                              <condition attribute='statecode' operator='eq' value='0' />
                                                              <condition attribute='lux_endorsementlibrary' operator='eq' uiname='' uitype='lux_endorsementlibrary' value='{Endorsement3.Attributes["lux_endorsementlibraryid"]}' />
                                                              <condition attribute='lux_application' operator='eq' uiname='' uitype='lux_propertyownersapplications' value='{appln.Id}' />
                                                            </filter>
                                                          </entity>
                                                        </fetch>";

                                if (FlatRoof == true) // Flat roof
                                {
                                    if (service.RetrieveMultiple(new FetchExpression(fetch1)).Entities.Count == 0)
                                    {
                                        Entity ent = new Entity("lux_applicationendorsements");
                                        ent["lux_isdefault"] = true;
                                        ent["lux_endorsementhtml"] = Endorsement3.Attributes.Contains("lux_endorsementhtml") ? Endorsement3.Attributes["lux_endorsementhtml"] : "";
                                        ent["lux_endorsementnumber"] = Endorsement3.Attributes["new_endorsementnumber"];
                                        ent["lux_name"] = Endorsement3.Attributes["lux_name"];
                                        ent["lux_endorsementlibrary"] = new EntityReference("lux_endorsementlibrary", new Guid(Endorsement3.Attributes["lux_endorsementlibraryid"].ToString()));
                                        ent["lux_application"] = new EntityReference("lux_propertyownersapplications", appln.Id);
                                        service.Create(ent);
                                    }
                                }
                                //else
                                //{
                                //    if (service.RetrieveMultiple(new FetchExpression(fetch1)).Entities.Count > 0)
                                //    {
                                //        service.Delete("lux_applicationendorsements", service.RetrieveMultiple(new FetchExpression(fetch1)).Entities[0].Id);
                                //    }
                                //}
                            }

                            var Endorsement4 = service.RetrieveMultiple(new FetchExpression(endorsementFetch)).Entities.FirstOrDefault(x => x.Attributes["lux_name"].ToString() == "Heat Application Condition");
                            if (Endorsement4 != null)
                            {
                                var fetch1 = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                                                          <entity name='lux_applicationendorsements'>
                                                            <attribute name='lux_applicationendorsementsid' />
                                                            <attribute name='lux_name' />
                                                            <attribute name='lux_endorsementnumber' />
                                                            <attribute name='createdon' />
                                                            <order attribute='lux_name' descending='false' />
                                                            <filter type='and'>
                                                              <condition attribute='statecode' operator='eq' value='0' />
                                                              <condition attribute='lux_endorsementlibrary' operator='eq' uiname='' uitype='lux_endorsementlibrary' value='{Endorsement4.Attributes["lux_endorsementlibraryid"]}' />
                                                              <condition attribute='lux_application' operator='eq' uiname='' uitype='lux_propertyownersapplications' value='{appln.Id}' />
                                                            </filter>
                                                          </entity>
                                                        </fetch>";

                                if (AnyHeatWorkAway == true)
                                {
                                    if (service.RetrieveMultiple(new FetchExpression(fetch1)).Entities.Count == 0)
                                    {
                                        Entity ent = new Entity("lux_applicationendorsements");
                                        ent["lux_isdefault"] = true;
                                        ent["lux_endorsementhtml"] = Endorsement4.Attributes.Contains("lux_endorsementhtml") ? Endorsement4.Attributes["lux_endorsementhtml"] : "";
                                        ent["lux_endorsementnumber"] = Endorsement4.Attributes["new_endorsementnumber"];
                                        ent["lux_name"] = Endorsement4.Attributes["lux_name"];
                                        ent["lux_endorsementlibrary"] = new EntityReference("lux_endorsementlibrary", new Guid(Endorsement4.Attributes["lux_endorsementlibraryid"].ToString()));
                                        ent["lux_application"] = new EntityReference("lux_propertyownersapplications", appln.Id);
                                        service.Create(ent);
                                    }
                                }
                                //else
                                //{
                                //    if (service.RetrieveMultiple(new FetchExpression(fetch1)).Entities.Count > 0)
                                //    {
                                //        service.Delete("lux_applicationendorsements", service.RetrieveMultiple(new FetchExpression(fetch1)).Entities[0].Id);
                                //    }
                                //}
                            }

                            var FloodScore = item.Attributes.Contains("lux_floodscore") ? item.GetAttributeValue<int>("lux_floodscore") : 0;
                            var SubsidienceScore = item.Attributes.Contains("lux_subsidencescore") ? item.GetAttributeValue<int>("lux_subsidencescore") : 0;

                            var Endorsement5 = service.RetrieveMultiple(new FetchExpression(endorsementFetch)).Entities.FirstOrDefault(x => x.Attributes["lux_name"].ToString() == "Increased Flood Excess - £1,000");
                            if (Endorsement5 != null)
                            {
                                var fetch1 = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                                                          <entity name='lux_applicationendorsements'>
                                                            <attribute name='lux_applicationendorsementsid' />
                                                            <attribute name='lux_name' />
                                                            <attribute name='lux_endorsementnumber' />
                                                            <attribute name='createdon' />
                                                            <order attribute='lux_name' descending='false' />
                                                            <filter type='and'>
                                                              <condition attribute='statecode' operator='eq' value='0' />
                                                              <condition attribute='lux_endorsementlibrary' operator='eq' uiname='' uitype='lux_endorsementlibrary' value='{Endorsement5.Attributes["lux_endorsementlibraryid"]}' />
                                                              <condition attribute='lux_application' operator='eq' uiname='' uitype='lux_propertyownersapplications' value='{appln.Id}' />
                                                            </filter>
                                                          </entity>
                                                        </fetch>";

                                //if (model.IsLive == 0)
                                //{
                                if (FloodScore == 10 || Basement == true) //£1000 Flood Excess
                                {
                                    if (service.RetrieveMultiple(new FetchExpression(fetch1)).Entities.Count == 0)
                                    {
                                        Entity ent = new Entity("lux_applicationendorsements");
                                        ent["lux_isdefault"] = true;
                                        ent["lux_endorsementnumber"] = Endorsement5.Attributes["new_endorsementnumber"];
                                        ent["lux_endorsementhtml"] = Endorsement5.Attributes["lux_endorsementhtml"];
                                        ent["lux_name"] = Endorsement5.Attributes["lux_name"];
                                        ent["lux_endorsementlibrary"] = new EntityReference("lux_endorsementlibrary", new Guid(Endorsement5.Attributes["lux_endorsementlibraryid"].ToString()));
                                        ent["lux_application"] = new EntityReference("lux_propertyownersapplications", appln.Id);
                                        service.Create(ent);
                                    }
                                }
                                //else
                                //{
                                //    if (service.RetrieveMultiple(new FetchExpression(fetch1)).Entities.Count > 0)
                                //    {
                                //        service.Delete("lux_applicationendorsements", service.RetrieveMultiple(new FetchExpression(fetch1)).Entities[0].Id);
                                //    }
                                //}
                                //}
                                //else
                                //{
                                //    if (FloodScore == 10) //£1000 Flood Excess
                                //    {
                                //        if (service.RetrieveMultiple(new FetchExpression(fetch1)).Entities.Count == 0)
                                //        {
                                //            Entity ent = new Entity("lux_applicationendorsements");
                                //            ent["lux_isdefault"] = true;
                                //            ent["lux_endorsementnumber"] = Endorsement5.Attributes["new_endorsementnumber"];
                                //            ent["lux_endorsementhtml"] = Endorsement5.Attributes["lux_endorsementhtml"];
                                //            ent["lux_name"] = Endorsement5.Attributes["lux_name"];
                                //            ent["lux_endorsementlibrary"] = new EntityReference("lux_endorsementlibrary", new Guid(Endorsement5.Attributes["lux_endorsementlibraryid"].ToString()));
                                //            ent["lux_application"] = new EntityReference("lux_propertyownersapplications", appln.Id);
                                //            service.Create(ent);
                                //        }
                                //    }
                                //    //else
                                //    //{
                                //    //    if (service.RetrieveMultiple(new FetchExpression(fetch1)).Entities.Count > 0)
                                //    //    {
                                //    //        service.Delete("lux_applicationendorsements", service.RetrieveMultiple(new FetchExpression(fetch1)).Entities[0].Id);
                                //    //    }
                                //    //}
                                //}
                            }

                            var Endorsement6 = service.RetrieveMultiple(new FetchExpression(endorsementFetch)).Entities.FirstOrDefault(x => x.Attributes["lux_name"].ToString() == "Flood Exclusion");
                            if (Endorsement6 != null)
                            {
                                var fetch1 = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                                                          <entity name='lux_applicationendorsements'>
                                                            <attribute name='lux_applicationendorsementsid' />
                                                            <attribute name='lux_name' />
                                                            <attribute name='lux_endorsementnumber' />
                                                            <attribute name='createdon' />
                                                            <order attribute='lux_name' descending='false' />
                                                            <filter type='and'>
                                                              <condition attribute='statecode' operator='eq' value='0' />
                                                              <condition attribute='lux_endorsementlibrary' operator='eq' uiname='' uitype='lux_endorsementlibrary' value='{Endorsement6.Attributes["lux_endorsementlibraryid"]}' />
                                                              <condition attribute='lux_application' operator='eq' uiname='' uitype='lux_propertyownersapplications' value='{appln.Id}' />
                                                            </filter>
                                                          </entity>
                                                        </fetch>";
                                if (FloodScore >= 11) //Decline - Exclude Flood Cover
                                {
                                    if (service.RetrieveMultiple(new FetchExpression(fetch1)).Entities.Count == 0)
                                    {
                                        Entity ent = new Entity("lux_applicationendorsements");
                                        ent["lux_isdefault"] = true;
                                        ent["lux_endorsementnumber"] = Endorsement6.Attributes["new_endorsementnumber"];
                                        ent["lux_endorsementhtml"] = Endorsement6.Attributes["lux_endorsementhtml"];
                                        ent["lux_name"] = Endorsement6.Attributes["lux_name"];
                                        ent["lux_endorsementlibrary"] = new EntityReference("lux_endorsementlibrary", new Guid(Endorsement6.Attributes["lux_endorsementlibraryid"].ToString()));
                                        ent["lux_application"] = new EntityReference("lux_propertyownersapplications", appln.Id);
                                        service.Create(ent);
                                    }
                                }
                                //else
                                //{
                                //    if (service.RetrieveMultiple(new FetchExpression(fetch1)).Entities.Count > 0)
                                //    {
                                //        service.Delete("lux_applicationendorsements", service.RetrieveMultiple(new FetchExpression(fetch1)).Entities[0].Id);
                                //    }
                                //}
                            }

                            //if (model.IsLive == 1)
                            //{
                            //    var Endorsement7 = service.RetrieveMultiple(new FetchExpression(endorsementFetch)).Entities.FirstOrDefault(x => x.Attributes["lux_name"].ToString() == "Increased Subsidence Excess - £2,500");
                            //    if (Endorsement7 != null)
                            //    {
                            //        var fetch1 = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                            //                          <entity name='lux_applicationendorsements'>
                            //                            <attribute name='lux_applicationendorsementsid' />
                            //                            <attribute name='lux_name' />
                            //                            <attribute name='lux_endorsementnumber' />
                            //                            <attribute name='createdon' />
                            //                            <order attribute='lux_name' descending='false' />
                            //                            <filter type='and'>
                            //                              <condition attribute='statecode' operator='eq' value='0' />
                            //                              <condition attribute='lux_endorsementlibrary' operator='eq' uiname='' uitype='lux_endorsementlibrary' value='{Endorsement7.Attributes["lux_endorsementlibraryid"]}' />
                            //                              <condition attribute='lux_application' operator='eq' uiname='' uitype='lux_propertyownersapplications' value='{appln.Id}' />
                            //                            </filter>
                            //                          </entity>
                            //                        </fetch>";
                            //        if ((SubsidienceScore == 4 || SubsidienceScore == 5) && item.GetAttributeValue<bool>("lux_issubsidencecoverrequired") == true) //£750 Flood Excess
                            //        {
                            //            if (service.RetrieveMultiple(new FetchExpression(fetch1)).Entities.Count == 0)
                            //            {
                            //                Entity ent = new Entity("lux_applicationendorsements");
                            //                ent["lux_isdefault"] = true;
                            //                ent["lux_endorsementnumber"] = Endorsement7.Attributes["new_endorsementnumber"];
                            //                ent["lux_endorsementhtml"] = Endorsement7.Attributes["lux_endorsementhtml"];
                            //                ent["lux_name"] = Endorsement7.Attributes["lux_name"];
                            //                ent["lux_endorsementlibrary"] = new EntityReference("lux_endorsementlibrary", new Guid(Endorsement7.Attributes["lux_endorsementlibraryid"].ToString()));
                            //                ent["lux_application"] = new EntityReference("lux_propertyownersapplications", appln.Id);
                            //                service.Create(ent);
                            //            }
                            //        }
                            //        //else
                            //        //{
                            //        //    if (service.RetrieveMultiple(new FetchExpression(fetch1)).Entities.Count > 0)
                            //        //    {
                            //        //        service.Delete("lux_applicationendorsements", service.RetrieveMultiple(new FetchExpression(fetch1)).Entities[0].Id);
                            //        //    }
                            //        //}
                            //    }

                            //    var Endorsement8 = service.RetrieveMultiple(new FetchExpression(endorsementFetch)).Entities.FirstOrDefault(x => x.Attributes["lux_name"].ToString() == "Increased Subsidence Excess - £5,000");
                            //    if (Endorsement8 != null)
                            //    {
                            //        var fetch1 = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                            //                          <entity name='lux_applicationendorsements'>
                            //                            <attribute name='lux_applicationendorsementsid' />
                            //                            <attribute name='lux_name' />
                            //                            <attribute name='lux_endorsementnumber' />
                            //                            <attribute name='createdon' />
                            //                            <order attribute='lux_name' descending='false' />
                            //                            <filter type='and'>
                            //                              <condition attribute='statecode' operator='eq' value='0' />
                            //                              <condition attribute='lux_endorsementlibrary' operator='eq' uiname='' uitype='lux_endorsementlibrary' value='{Endorsement8.Attributes["lux_endorsementlibraryid"]}' />
                            //                             <condition attribute='lux_application' operator='eq' uiname='' uitype='lux_propertyownersapplications' value='{appln.Id}' />
                            //                            </filter>
                            //                          </entity>
                            //                        </fetch>";
                            //        if (SubsidienceScore == 6 && item.GetAttributeValue<bool>("lux_issubsidencecoverrequired") == true) //£750 Flood Excess
                            //        {
                            //            if (service.RetrieveMultiple(new FetchExpression(fetch1)).Entities.Count == 0)
                            //            {
                            //                Entity ent = new Entity("lux_applicationendorsements");
                            //                ent["lux_isdefault"] = true;
                            //                ent["lux_endorsementnumber"] = Endorsement8.Attributes["new_endorsementnumber"];
                            //                ent["lux_endorsementhtml"] = Endorsement8.Attributes["lux_endorsementhtml"];
                            //                ent["lux_name"] = Endorsement8.Attributes["lux_name"];
                            //                ent["lux_endorsementlibrary"] = new EntityReference("lux_endorsementlibrary", new Guid(Endorsement8.Attributes["lux_endorsementlibraryid"].ToString()));
                            //                ent["lux_application"] = new EntityReference("lux_propertyownersapplications", appln.Id);
                            //                service.Create(ent);
                            //            }
                            //        }
                            //        //else
                            //        //{
                            //        //    if (service.RetrieveMultiple(new FetchExpression(fetch1)).Entities.Count > 0)
                            //        //    {
                            //        //        service.Delete("lux_applicationendorsements", service.RetrieveMultiple(new FetchExpression(fetch1)).Entities[0].Id);
                            //        //    }
                            //        //}
                            //    }
                            //}
                            //else
                            //{
                            var Endorsement7 = service.RetrieveMultiple(new FetchExpression(endorsementFetch)).Entities.FirstOrDefault(x => x.Attributes["lux_name"].ToString() == "Increased Subsidence Excess - £2,500");
                            if (Endorsement7 != null)
                            {
                                var fetch1 = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                                                          <entity name='lux_applicationendorsements'>
                                                            <attribute name='lux_applicationendorsementsid' />
                                                            <attribute name='lux_name' />
                                                            <attribute name='lux_endorsementnumber' />
                                                            <attribute name='createdon' />
                                                            <order attribute='lux_name' descending='false' />
                                                            <filter type='and'>
                                                              <condition attribute='statecode' operator='eq' value='0' />
                                                              <condition attribute='lux_endorsementlibrary' operator='eq' uiname='' uitype='lux_endorsementlibrary' value='{Endorsement7.Attributes["lux_endorsementlibraryid"]}' />
                                                              <condition attribute='lux_application' operator='eq' uiname='' uitype='lux_propertyownersapplications' value='{appln.Id}' />
                                                            </filter>
                                                          </entity>
                                                        </fetch>";
                                if ((SubsidienceScore == 6) && item.GetAttributeValue<bool>("lux_issubsidencecoverrequired") == true) //£750 Flood Excess
                                {
                                    if (service.RetrieveMultiple(new FetchExpression(fetch1)).Entities.Count == 0)
                                    {
                                        Entity ent = new Entity("lux_applicationendorsements");
                                        ent["lux_isdefault"] = true;
                                        ent["lux_endorsementnumber"] = Endorsement7.Attributes["new_endorsementnumber"];
                                        ent["lux_endorsementhtml"] = Endorsement7.Attributes["lux_endorsementhtml"];
                                        ent["lux_name"] = Endorsement7.Attributes["lux_name"];
                                        ent["lux_endorsementlibrary"] = new EntityReference("lux_endorsementlibrary", new Guid(Endorsement7.Attributes["lux_endorsementlibraryid"].ToString()));
                                        ent["lux_application"] = new EntityReference("lux_propertyownersapplications", appln.Id);
                                        service.Create(ent);
                                    }
                                }
                                //else
                                //{
                                //    if (service.RetrieveMultiple(new FetchExpression(fetch1)).Entities.Count > 0)
                                //    {
                                //        service.Delete("lux_applicationendorsements", service.RetrieveMultiple(new FetchExpression(fetch1)).Entities[0].Id);
                                //    }
                                //}
                            }
                            //}

                            var Endorsement10 = service.RetrieveMultiple(new FetchExpression(endorsementFetch)).Entities.FirstOrDefault(x => x.Attributes["lux_name"].ToString() == "Subsidence Exclusion");
                            if (Endorsement10 != null)
                            {
                                var fetch1 = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                                                          <entity name='lux_applicationendorsements'>
                                                            <attribute name='lux_applicationendorsementsid' />
                                                            <attribute name='lux_name' />
                                                            <attribute name='lux_endorsementnumber' />
                                                            <attribute name='createdon' />
                                                            <order attribute='lux_name' descending='false' />
                                                            <filter type='and'>
                                                              <condition attribute='statecode' operator='eq' value='0' />
                                                              <condition attribute='lux_endorsementlibrary' operator='eq' uiname='' uitype='lux_endorsementlibrary' value='{Endorsement10.Attributes["lux_endorsementlibraryid"]}' />
                                                              <condition attribute='lux_application' operator='eq' uiname='' uitype='lux_propertyownersapplications' value='{appln.Id}' />
                                                            </filter>
                                                          </entity>
                                                        </fetch>";
                                if (SubsidienceScore == 7 && item.GetAttributeValue<bool>("lux_issubsidencecoverrequired") == true) //£750 Flood Excess
                                {
                                    if (service.RetrieveMultiple(new FetchExpression(fetch1)).Entities.Count == 0)
                                    {
                                        Entity ent = new Entity("lux_applicationendorsements");
                                        ent["lux_isdefault"] = true;
                                        ent["lux_endorsementnumber"] = Endorsement10.Attributes["new_endorsementnumber"];
                                        ent["lux_endorsementhtml"] = Endorsement10.Attributes["lux_endorsementhtml"];
                                        ent["lux_name"] = Endorsement10.Attributes["lux_name"];
                                        ent["lux_endorsementlibrary"] = new EntityReference("lux_endorsementlibrary", new Guid(Endorsement10.Attributes["lux_endorsementlibraryid"].ToString()));
                                        ent["lux_application"] = new EntityReference("lux_propertyownersapplications", appln.Id);
                                        service.Create(ent);
                                    }
                                }
                                //else
                                //{
                                //    if (service.RetrieveMultiple(new FetchExpression(fetch1)).Entities.Count > 0)
                                //    {
                                //        service.Delete("lux_applicationendorsements", service.RetrieveMultiple(new FetchExpression(fetch1)).Entities[0].Id);
                                //    }
                                //}
                            }

                            var Endorsement11 = service.RetrieveMultiple(new FetchExpression(endorsementFetch)).Entities.FirstOrDefault(x => x.Attributes["lux_name"].ToString() == "Increased Flood Excess - £500");
                            if (Endorsement11 != null)
                            {
                                var fetch1 = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                                                          <entity name='lux_applicationendorsements'>
                                                            <attribute name='lux_applicationendorsementsid' />
                                                            <attribute name='lux_name' />
                                                            <attribute name='lux_endorsementnumber' />
                                                            <attribute name='createdon' />
                                                            <order attribute='lux_name' descending='false' />
                                                            <filter type='and'>
                                                              <condition attribute='statecode' operator='eq' value='0' />
                                                              <condition attribute='lux_endorsementlibrary' operator='eq' uiname='' uitype='lux_endorsementlibrary' value='{Endorsement11.Attributes["lux_endorsementlibraryid"]}' />
                                                              <condition attribute='lux_application' operator='eq' uiname='' uitype='lux_propertyownersapplications' value='{appln.Id}' />
                                                            </filter>
                                                          </entity>
                                                        </fetch>";
                                if (FloodScore == 9) //£500 Flood Excess
                                {
                                    if (service.RetrieveMultiple(new FetchExpression(fetch1)).Entities.Count == 0)
                                    {
                                        Entity ent = new Entity("lux_applicationendorsements");
                                        ent["lux_isdefault"] = true;
                                        ent["lux_endorsementnumber"] = Endorsement11.Attributes["new_endorsementnumber"];
                                        ent["lux_endorsementhtml"] = Endorsement11.Attributes["lux_endorsementhtml"];
                                        ent["lux_name"] = Endorsement11.Attributes["lux_name"];
                                        ent["lux_endorsementlibrary"] = new EntityReference("lux_endorsementlibrary", new Guid(Endorsement11.Attributes["lux_endorsementlibraryid"].ToString()));
                                        ent["lux_application"] = new EntityReference("lux_propertyownersapplications", appln.Id);
                                        service.Create(ent);
                                    }
                                }
                                //else
                                //{
                                //    if (service.RetrieveMultiple(new FetchExpression(fetch1)).Entities.Count > 0)
                                //    {
                                //        service.Delete("lux_applicationendorsements", service.RetrieveMultiple(new FetchExpression(fetch1)).Entities[0].Id);
                                //    }
                                //}
                            }

                            var Endorsement12 = service.RetrieveMultiple(new FetchExpression(endorsementFetch)).Entities.FirstOrDefault(x => x.Attributes["lux_name"].ToString() == "Work Away Exclusion - Employers Liability");
                            if (Endorsement12 != null)
                            {
                                var fetch1 = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                                                          <entity name='lux_applicationendorsements'>
                                                            <attribute name='lux_applicationendorsementsid' />
                                                            <attribute name='lux_name' />
                                                            <attribute name='lux_endorsementnumber' />
                                                            <attribute name='createdon' />
                                                            <order attribute='lux_name' descending='false' />
                                                            <filter type='and'>
                                                              <condition attribute='statecode' operator='eq' value='0' />
                                                              <condition attribute='lux_endorsementlibrary' operator='eq' uiname='' uitype='lux_endorsementlibrary' value='{Endorsement12.Attributes["lux_endorsementlibraryid"]}' />
                                                              <condition attribute='lux_application' operator='eq' uiname='' uitype='lux_propertyownersapplications' value='{appln.Id}' />
                                                            </filter>
                                                          </entity>
                                                        </fetch>";

                                if (WorkAway == false)
                                {
                                    if (service.RetrieveMultiple(new FetchExpression(fetch1)).Entities.Count == 0)
                                    {
                                        Entity ent = new Entity("lux_applicationendorsements");
                                        ent["lux_isdefault"] = true;
                                        ent["lux_endorsementhtml"] = Endorsement12.Attributes.Contains("lux_endorsementhtml") ? Endorsement12.Attributes["lux_endorsementhtml"] : "";
                                        ent["lux_endorsementnumber"] = Endorsement12.Attributes["new_endorsementnumber"];
                                        ent["lux_name"] = Endorsement12.Attributes["lux_name"];
                                        ent["lux_endorsementlibrary"] = new EntityReference("lux_endorsementlibrary", new Guid(Endorsement12.Attributes["lux_endorsementlibraryid"].ToString()));
                                        ent["lux_application"] = new EntityReference("lux_propertyownersapplications", appln.Id);
                                        service.Create(ent);
                                    }
                                }
                                //else
                                //{
                                //    if (service.RetrieveMultiple(new FetchExpression(fetch1)).Entities.Count > 0)
                                //    {
                                //        service.Delete("lux_applicationendorsements", service.RetrieveMultiple(new FetchExpression(fetch1)).Entities[0].Id);
                                //    }
                                //}
                            }

                            var Endorsement13 = service.RetrieveMultiple(new FetchExpression(endorsementFetch)).Entities.FirstOrDefault(x => x.Attributes["lux_name"].ToString() == "Work Away Exclusion - Public and Products Liability");
                            if (Endorsement13 != null)
                            {
                                var fetch1 = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                                                          <entity name='lux_applicationendorsements'>
                                                            <attribute name='lux_applicationendorsementsid' />
                                                            <attribute name='lux_name' />
                                                            <attribute name='lux_endorsementnumber' />
                                                            <attribute name='createdon' />
                                                            <order attribute='lux_name' descending='false' />
                                                            <filter type='and'>
                                                              <condition attribute='statecode' operator='eq' value='0' />
                                                              <condition attribute='lux_endorsementlibrary' operator='eq' uiname='' uitype='lux_endorsementlibrary' value='{Endorsement13.Attributes["lux_endorsementlibraryid"]}' />
                                                              <condition attribute='lux_application' operator='eq' uiname='' uitype='lux_propertyownersapplications' value='{appln.Id}' />
                                                            </filter>
                                                          </entity>
                                                        </fetch>";

                                if (WorkAway == false)
                                {
                                    if (service.RetrieveMultiple(new FetchExpression(fetch1)).Entities.Count == 0)
                                    {
                                        Entity ent = new Entity("lux_applicationendorsements");
                                        ent["lux_isdefault"] = true;
                                        ent["lux_endorsementhtml"] = Endorsement13.Attributes.Contains("lux_endorsementhtml") ? Endorsement13.Attributes["lux_endorsementhtml"] : "";
                                        ent["lux_endorsementnumber"] = Endorsement13.Attributes["new_endorsementnumber"];
                                        ent["lux_name"] = Endorsement13.Attributes["lux_name"];
                                        ent["lux_endorsementlibrary"] = new EntityReference("lux_endorsementlibrary", new Guid(Endorsement13.Attributes["lux_endorsementlibraryid"].ToString()));
                                        ent["lux_application"] = new EntityReference("lux_propertyownersapplications", appln.Id);
                                        service.Create(ent);
                                    }
                                }
                                //else
                                //{
                                //    if (service.RetrieveMultiple(new FetchExpression(fetch1)).Entities.Count > 0)
                                //    {
                                //        service.Delete("lux_applicationendorsements", service.RetrieveMultiple(new FetchExpression(fetch1)).Entities[0].Id);
                                //    }
                                //}
                            }

                            var Endorsement14 = service.RetrieveMultiple(new FetchExpression(endorsementFetch)).Entities.FirstOrDefault(x => x.Attributes["lux_name"].ToString() == "Alarm Requirement: Audible Signalling");
                            if (Endorsement14 != null)
                            {
                                var fetch1 = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                                                          <entity name='lux_applicationendorsements'>
                                                            <attribute name='lux_applicationendorsementsid' />
                                                            <attribute name='lux_name' />
                                                            <attribute name='lux_endorsementnumber' />
                                                            <attribute name='createdon' />
                                                            <order attribute='lux_name' descending='false' />
                                                            <filter type='and'>
                                                              <condition attribute='statecode' operator='eq' value='0' />
                                                              <condition attribute='lux_endorsementlibrary' operator='eq' uiname='' uitype='lux_endorsementlibrary' value='{Endorsement14.Attributes["lux_endorsementlibraryid"]}' />
                                                              <condition attribute='lux_application' operator='eq' uiname='' uitype='lux_propertyownersapplications' value='{appln.Id}' />
                                                            </filter>
                                                          </entity>
                                                        </fetch>";

                                if (IntruderAlarm == true && TypeOFAlarm == 972970001)//audible only
                                {
                                    if (service.RetrieveMultiple(new FetchExpression(fetch1)).Entities.Count == 0)
                                    {
                                        Entity ent = new Entity("lux_applicationendorsements");
                                        ent["lux_isdefault"] = true;
                                        ent["lux_endorsementhtml"] = Endorsement14.Attributes.Contains("lux_endorsementhtml") ? Endorsement14.Attributes["lux_endorsementhtml"] : "";
                                        ent["lux_endorsementnumber"] = Endorsement14.Attributes["new_endorsementnumber"];
                                        ent["lux_name"] = Endorsement14.Attributes["lux_name"];
                                        ent["lux_endorsementlibrary"] = new EntityReference("lux_endorsementlibrary", new Guid(Endorsement14.Attributes["lux_endorsementlibraryid"].ToString()));
                                        ent["lux_application"] = new EntityReference("lux_propertyownersapplications", appln.Id);
                                        service.Create(ent);
                                    }
                                }
                                //else
                                //{
                                //    if (service.RetrieveMultiple(new FetchExpression(fetch1)).Entities.Count > 0)
                                //    {
                                //        service.Delete("lux_applicationendorsements", service.RetrieveMultiple(new FetchExpression(fetch1)).Entities[0].Id);
                                //    }
                                //}
                            }

                            var Endorsement15 = service.RetrieveMultiple(new FetchExpression(endorsementFetch)).Entities.FirstOrDefault(x => x.Attributes["lux_name"].ToString() == "Alarm Requirement: Non-Confirmed Signalling");
                            if (Endorsement15 != null)
                            {
                                var fetch1 = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                                                          <entity name='lux_applicationendorsements'>
                                                            <attribute name='lux_applicationendorsementsid' />
                                                            <attribute name='lux_name' />
                                                            <attribute name='lux_endorsementnumber' />
                                                            <attribute name='createdon' />
                                                            <order attribute='lux_name' descending='false' />
                                                            <filter type='and'>
                                                              <condition attribute='statecode' operator='eq' value='0' />
                                                              <condition attribute='lux_endorsementlibrary' operator='eq' uiname='' uitype='lux_endorsementlibrary' value='{Endorsement15.Attributes["lux_endorsementlibraryid"]}' />
                                                              <condition attribute='lux_application' operator='eq' uiname='' uitype='lux_propertyownersapplications' value='{appln.Id}' />
                                                            </filter>
                                                          </entity>
                                                        </fetch>";

                                if (IntruderAlarm == true && (TypeOFAlarm == 972970002 || TypeOFAlarm == 972970005))
                                {
                                    if (service.RetrieveMultiple(new FetchExpression(fetch1)).Entities.Count == 0)
                                    {
                                        Entity ent = new Entity("lux_applicationendorsements");
                                        ent["lux_isdefault"] = true;
                                        ent["lux_endorsementhtml"] = Endorsement15.Attributes.Contains("lux_endorsementhtml") ? Endorsement15.Attributes["lux_endorsementhtml"] : "";
                                        ent["lux_endorsementnumber"] = Endorsement15.Attributes["new_endorsementnumber"];
                                        ent["lux_name"] = Endorsement15.Attributes["lux_name"];
                                        ent["lux_endorsementlibrary"] = new EntityReference("lux_endorsementlibrary", new Guid(Endorsement15.Attributes["lux_endorsementlibraryid"].ToString()));
                                        ent["lux_application"] = new EntityReference("lux_propertyownersapplications", appln.Id);
                                        service.Create(ent);
                                    }
                                }
                                //else
                                //{
                                //    if (service.RetrieveMultiple(new FetchExpression(fetch1)).Entities.Count > 0)
                                //    {
                                //        service.Delete("lux_applicationendorsements", service.RetrieveMultiple(new FetchExpression(fetch1)).Entities[0].Id);
                                //    }
                                //}
                            }

                            var Endorsement16 = service.RetrieveMultiple(new FetchExpression(endorsementFetch)).Entities.FirstOrDefault(x => x.Attributes["lux_name"].ToString() == "Alarm Requirement: Confirmed Signalling");
                            if (Endorsement16 != null)
                            {
                                var fetch1 = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                                                          <entity name='lux_applicationendorsements'>
                                                            <attribute name='lux_applicationendorsementsid' />
                                                            <attribute name='lux_name' />
                                                            <attribute name='lux_endorsementnumber' />
                                                            <attribute name='createdon' />
                                                            <order attribute='lux_name' descending='false' />
                                                            <filter type='and'>
                                                              <condition attribute='statecode' operator='eq' value='0' />
                                                              <condition attribute='lux_endorsementlibrary' operator='eq' uiname='' uitype='lux_endorsementlibrary' value='{Endorsement16.Attributes["lux_endorsementlibraryid"]}' />
                                                              <condition attribute='lux_application' operator='eq' uiname='' uitype='lux_propertyownersapplications' value='{appln.Id}' />
                                                            </filter>
                                                          </entity>
                                                        </fetch>";

                                if (IntruderAlarm == true && (TypeOFAlarm == 972970003 || TypeOFAlarm == 972970004))//redcare gsm or dualcom
                                {
                                    if (service.RetrieveMultiple(new FetchExpression(fetch1)).Entities.Count == 0)
                                    {
                                        Entity ent = new Entity("lux_applicationendorsements");
                                        ent["lux_isdefault"] = true;
                                        ent["lux_endorsementhtml"] = Endorsement16.Attributes.Contains("lux_endorsementhtml") ? Endorsement16.Attributes["lux_endorsementhtml"] : "";
                                        ent["lux_endorsementnumber"] = Endorsement16.Attributes["new_endorsementnumber"];
                                        ent["lux_name"] = Endorsement16.Attributes["lux_name"];
                                        ent["lux_endorsementlibrary"] = new EntityReference("lux_endorsementlibrary", new Guid(Endorsement16.Attributes["lux_endorsementlibraryid"].ToString()));
                                        ent["lux_application"] = new EntityReference("lux_propertyownersapplications", appln.Id);
                                        service.Create(ent);
                                    }
                                }
                                //else
                                //{
                                //    if (service.RetrieveMultiple(new FetchExpression(fetch1)).Entities.Count > 0)
                                //    {
                                //        service.Delete("lux_applicationendorsements", service.RetrieveMultiple(new FetchExpression(fetch1)).Entities[0].Id);
                                //    }
                                //}
                            }

                            var Endorsement17 = service.RetrieveMultiple(new FetchExpression(endorsementFetch)).Entities.FirstOrDefault(x => x.Attributes["lux_name"].ToString() == "Treatment Liability Extension");
                            if (Endorsement17 != null)
                            {
                                var fetch1 = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                                                          <entity name='lux_applicationendorsements'>
                                                            <attribute name='lux_applicationendorsementsid' />
                                                            <attribute name='lux_name' />
                                                            <attribute name='lux_endorsementnumber' />
                                                            <attribute name='createdon' />
                                                            <order attribute='lux_name' descending='false' />
                                                            <filter type='and'>
                                                              <condition attribute='statecode' operator='eq' value='0' />
                                                              <condition attribute='lux_endorsementlibrary' operator='eq' uiname='' uitype='lux_endorsementlibrary' value='{Endorsement17.Attributes["lux_endorsementlibraryid"]}' />
                                                              <condition attribute='lux_application' operator='eq' uiname='' uitype='lux_propertyownersapplications' value='{appln.Id}' />
                                                            </filter>
                                                          </entity>
                                                        </fetch>";

                                if (PrimaryTrade.ToLower() == "hairdressing" || PrimaryTrade.ToLower() == "barber" || PrimaryTrade.ToLower() == "beauty salon" || PrimaryTrade.ToLower() == "beauty therapy" || PrimaryTrade.ToLower() == "beautician" ||
                                    SecondaryTrade.ToLower() == "hairdressing" || SecondaryTrade.ToLower() == "barber" || SecondaryTrade.ToLower() == "beauty salon" || SecondaryTrade.ToLower() == "beauty therapy" || SecondaryTrade.ToLower() == "beautician")
                                {
                                    if (service.RetrieveMultiple(new FetchExpression(fetch1)).Entities.Count == 0)
                                    {
                                        Entity ent = new Entity("lux_applicationendorsements");
                                        ent["lux_isdefault"] = true;
                                        ent["lux_endorsementnumber"] = Endorsement17.Attributes["new_endorsementnumber"];
                                        ent["lux_endorsementhtml"] = Endorsement17.Attributes.Contains("lux_endorsementhtml") ? Endorsement17.Attributes["lux_endorsementhtml"] : "";
                                        ent["lux_name"] = Endorsement17.Attributes["lux_name"];
                                        ent["lux_endorsementlibrary"] = new EntityReference("lux_endorsementlibrary", new Guid(Endorsement17.Attributes["lux_endorsementlibraryid"].ToString()));
                                        ent["lux_application"] = new EntityReference("lux_propertyownersapplications", appln.Id);
                                        service.Create(ent);
                                    }
                                }
                                //else
                                //{
                                //    if (service.RetrieveMultiple(new FetchExpression(fetch1)).Entities.Count > 0)
                                //    {
                                //        service.Delete("lux_applicationendorsements", service.RetrieveMultiple(new FetchExpression(fetch1)).Entities[0].Id);
                                //    }
                                //}
                            }

                            var Endorsement18 = service.RetrieveMultiple(new FetchExpression(endorsementFetch)).Entities.FirstOrDefault(x => x.Attributes["lux_name"].ToString() == "Gym and Fitness Clubs Condition");
                            if (Endorsement18 != null)
                            {
                                var fetch1 = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                                                          <entity name='lux_applicationendorsements'>
                                                            <attribute name='lux_applicationendorsementsid' />
                                                            <attribute name='lux_name' />
                                                            <attribute name='lux_endorsementnumber' />
                                                            <attribute name='createdon' />
                                                            <order attribute='lux_name' descending='false' />
                                                            <filter type='and'>
                                                              <condition attribute='statecode' operator='eq' value='0' />
                                                              <condition attribute='lux_endorsementlibrary' operator='eq' uiname='' uitype='lux_endorsementlibrary' value='{Endorsement18.Attributes["lux_endorsementlibraryid"]}' />
                                                              <condition attribute='lux_application' operator='eq' uiname='' uitype='lux_propertyownersapplications' value='{appln.Id}' />
                                                            </filter>
                                                          </entity>
                                                        </fetch>";

                                if (PrimaryTrade.ToLower() == "gymnasium" || PrimaryTrade.ToLower() == "health club" || PrimaryTrade.ToLower() == "leisure centre" || PrimaryTrade.ToLower() == "sports centre" ||
                                    SecondaryTrade.ToLower() == "gymnasium" || SecondaryTrade.ToLower() == "health club" || SecondaryTrade.ToLower() == "leisure centre" || SecondaryTrade.ToLower() == "sports centre")
                                {
                                    if (service.RetrieveMultiple(new FetchExpression(fetch1)).Entities.Count == 0)
                                    {
                                        Entity ent = new Entity("lux_applicationendorsements");
                                        ent["lux_isdefault"] = true;
                                        ent["lux_endorsementnumber"] = Endorsement18.Attributes["new_endorsementnumber"];
                                        ent["lux_endorsementhtml"] = Endorsement18.Attributes.Contains("lux_endorsementhtml") ? Endorsement18.Attributes["lux_endorsementhtml"] : "";
                                        ent["lux_name"] = Endorsement18.Attributes["lux_name"];
                                        ent["lux_endorsementlibrary"] = new EntityReference("lux_endorsementlibrary", new Guid(Endorsement18.Attributes["lux_endorsementlibraryid"].ToString()));
                                        ent["lux_application"] = new EntityReference("lux_propertyownersapplications", appln.Id);
                                        service.Create(ent);
                                    }
                                }
                                //else
                                //{
                                //    if (service.RetrieveMultiple(new FetchExpression(fetch1)).Entities.Count > 0)
                                //    {
                                //        service.Delete("lux_applicationendorsements", service.RetrieveMultiple(new FetchExpression(fetch1)).Entities[0].Id);
                                //    }
                                //}
                            }

                            var Endorsement19 = service.RetrieveMultiple(new FetchExpression(endorsementFetch)).Entities.FirstOrDefault(x => x.Attributes["lux_name"].ToString() == "Products Exclusion");
                            if (Endorsement19 != null)
                            {
                                var fetch1 = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                                                          <entity name='lux_applicationendorsements'>
                                                            <attribute name='lux_applicationendorsementsid' />
                                                            <attribute name='lux_name' />
                                                            <attribute name='lux_endorsementnumber' />
                                                            <attribute name='createdon' />
                                                            <order attribute='lux_name' descending='false' />
                                                            <filter type='and'>
                                                              <condition attribute='statecode' operator='eq' value='0' />
                                                              <condition attribute='lux_endorsementlibrary' operator='eq' uiname='' uitype='lux_endorsementlibrary' value='{Endorsement19.Attributes["lux_endorsementlibraryid"]}' />
                                                              <condition attribute='lux_application' operator='eq' uiname='' uitype='lux_propertyownersapplications' value='{appln.Id}' />
                                                            </filter>
                                                          </entity>
                                                        </fetch>";

                                if (ProdusHazardGrade >= 6)
                                {
                                    if (service.RetrieveMultiple(new FetchExpression(fetch1)).Entities.Count == 0)
                                    {
                                        Entity ent = new Entity("lux_applicationendorsements");
                                        ent["lux_isdefault"] = true;
                                        ent["lux_endorsementnumber"] = Endorsement19.Attributes["new_endorsementnumber"];
                                        ent["lux_endorsementhtml"] = Endorsement19.Attributes["lux_endorsementhtml"];
                                        ent["lux_name"] = Endorsement19.Attributes["lux_name"];
                                        ent["lux_endorsementlibrary"] = new EntityReference("lux_endorsementlibrary", new Guid(Endorsement19.Attributes["lux_endorsementlibraryid"].ToString()));
                                        ent["lux_application"] = new EntityReference("lux_propertyownersapplications", appln.Id);
                                        service.Create(ent);
                                    }
                                }
                                //else
                                //{
                                //    if (service.RetrieveMultiple(new FetchExpression(fetch1)).Entities.Count > 0)
                                //    {
                                //        service.Delete("lux_applicationendorsements", service.RetrieveMultiple(new FetchExpression(fetch1)).Entities[0].Id);
                                //    }
                                //}
                            }

                            var Endorsement20 = service.RetrieveMultiple(new FetchExpression(endorsementFetch)).Entities.FirstOrDefault(x => x.Attributes["lux_name"].ToString() == "Waste Condition - Bakers");
                            if (Endorsement20 != null)
                            {
                                var fetch1 = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                                                          <entity name='lux_applicationendorsements'>
                                                            <attribute name='lux_applicationendorsementsid' />
                                                            <attribute name='lux_name' />
                                                            <attribute name='lux_endorsementnumber' />
                                                            <attribute name='createdon' />
                                                            <order attribute='lux_name' descending='false' />
                                                            <filter type='and'>
                                                              <condition attribute='statecode' operator='eq' value='0' />
                                                              <condition attribute='lux_endorsementlibrary' operator='eq' uiname='' uitype='lux_endorsementlibrary' value='{Endorsement20.Attributes["lux_endorsementlibraryid"]}' />
                                                              <condition attribute='lux_application' operator='eq' uiname='' uitype='lux_propertyownersapplications' value='{appln.Id}' />
                                                            </filter>
                                                          </entity>
                                                        </fetch>";

                                if (PrimaryTrade.ToLower() == "bakery" || PrimaryTrade.ToLower() == "baker" || PrimaryTrade.ToLower() == "cake making and decorating" ||
                                    SecondaryTrade.ToLower() == "bakery" || SecondaryTrade.ToLower() == "baker" || SecondaryTrade.ToLower() == "cake making and decorating")
                                {
                                    if (service.RetrieveMultiple(new FetchExpression(fetch1)).Entities.Count == 0)
                                    {
                                        Entity ent = new Entity("lux_applicationendorsements");
                                        ent["lux_application"] = new EntityReference("lux_propertyownersapplications", appln.Id);
                                        ent["lux_isdefault"] = true;
                                        ent["lux_endorsementnumber"] = Endorsement20.Attributes["new_endorsementnumber"];
                                        ent["lux_endorsementhtml"] = Endorsement20.Attributes["lux_endorsementhtml"];
                                        ent["lux_name"] = Endorsement20.Attributes["lux_name"];
                                        ent["lux_endorsementlibrary"] = new EntityReference("lux_endorsementlibrary", new Guid(Endorsement20.Attributes["lux_endorsementlibraryid"].ToString()));
                                        service.Create(ent);
                                    }
                                }
                                //else
                                //{
                                //    if (service.RetrieveMultiple(new FetchExpression(fetch1)).Entities.Count > 0)
                                //    {
                                //        service.Delete("lux_applicationendorsements", service.RetrieveMultiple(new FetchExpression(fetch1)).Entities[0].Id);
                                //    }
                                //}
                            }

                            var Endorsement21 = service.RetrieveMultiple(new FetchExpression(endorsementFetch)).Entities.FirstOrDefault(x => x.Attributes["lux_name"].ToString() == "Unattended Machinery Condition");
                            if (Endorsement21 != null)
                            {
                                var fetch1 = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                                                          <entity name='lux_applicationendorsements'>
                                                            <attribute name='lux_applicationendorsementsid' />
                                                            <attribute name='lux_name' />
                                                            <attribute name='lux_endorsementnumber' />
                                                            <attribute name='createdon' />
                                                            <order attribute='lux_name' descending='false' />
                                                            <filter type='and'>
                                                              <condition attribute='statecode' operator='eq' value='0' />
                                                              <condition attribute='lux_endorsementlibrary' operator='eq' uiname='' uitype='lux_endorsementlibrary' value='{Endorsement21.Attributes["lux_endorsementlibraryid"]}' />
                                                              <condition attribute='lux_application' operator='eq' uiname='' uitype='lux_propertyownersapplications' value='{appln.Id}' />
                                                            </filter>
                                                          </entity>
                                                        </fetch>";

                                if (PrimaryTrade.ToLower() == "bakery" || PrimaryTrade.ToLower() == "baker" || PrimaryTrade.ToLower() == "cake making and decorating" ||
                                    SecondaryTrade.ToLower() == "bakery" || SecondaryTrade.ToLower() == "baker" || SecondaryTrade.ToLower() == "cake making and decorating")
                                {
                                    if (service.RetrieveMultiple(new FetchExpression(fetch1)).Entities.Count == 0)
                                    {
                                        Entity ent = new Entity("lux_applicationendorsements");
                                        ent["lux_application"] = new EntityReference("lux_propertyownersapplications", appln.Id);
                                        ent["lux_isdefault"] = true;
                                        ent["lux_endorsementnumber"] = Endorsement21.Attributes["new_endorsementnumber"];
                                        ent["lux_endorsementhtml"] = Endorsement21.Attributes["lux_endorsementhtml"];
                                        ent["lux_name"] = Endorsement21.Attributes["lux_name"];
                                        ent["lux_endorsementlibrary"] = new EntityReference("lux_endorsementlibrary", new Guid(Endorsement21.Attributes["lux_endorsementlibraryid"].ToString()));
                                        service.Create(ent);
                                    }
                                }
                                //else
                                //{
                                //    if (service.RetrieveMultiple(new FetchExpression(fetch1)).Entities.Count > 0)
                                //    {
                                //        service.Delete("lux_applicationendorsements", service.RetrieveMultiple(new FetchExpression(fetch1)).Entities[0].Id);
                                //    }
                                //}
                            }

                            var Endorsement9 = service.RetrieveMultiple(new FetchExpression(endorsementFetch)).Entities.FirstOrDefault(x => x.Attributes["lux_name"].ToString() == "Flat Roof - Increased Excess");
                            if (Endorsement9 != null)
                            {
                                var percent = item.Attributes.Contains("lux_flatroofpercentage") ? item.Attributes["lux_flatroofpercentage"].ToString().Replace("%", "") : "0";
                                percent = Regex.Match(percent, @"\d+").Value;

                                var fetch1 = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                                                              <entity name='lux_applicationendorsements'>
                                                                <attribute name='lux_applicationendorsementsid' />
                                                                <attribute name='lux_name' />
                                                                <attribute name='lux_endorsementnumber' />
                                                                <attribute name='createdon' />
                                                                <order attribute='lux_name' descending='false' />
                                                                <filter type='and'>
                                                                  <condition attribute='statecode' operator='eq' value='0' />
                                                                  <condition attribute='lux_endorsementlibrary' operator='eq' uiname='Wood Burner Condition' uitype='lux_endorsementlibrary' value='{Endorsement9.Attributes["lux_endorsementlibraryid"]}' />
                                                                  <condition attribute='lux_application' operator='eq' uiname='' uitype='lux_propertyownersapplications' value='{appln.Id}' />
                                                                </filter>
                                                              </entity>
                                                            </fetch>";

                                if (FlatRoof == true && Convert.ToDecimal(percent) > 50) // Flat roof
                                {
                                    if (service.RetrieveMultiple(new FetchExpression(fetch1)).Entities.Count == 0)
                                    {
                                        Entity ent = new Entity("lux_applicationendorsements");
                                        ent["lux_isdefault"] = true;
                                        ent["lux_endorsementhtml"] = Endorsement9.Attributes["lux_endorsementhtml"];
                                        ent["lux_endorsementnumber"] = Endorsement9.Attributes["new_endorsementnumber"];
                                        ent["lux_name"] = Endorsement9.Attributes["lux_name"];
                                        ent["lux_endorsementlibrary"] = new EntityReference("lux_endorsementlibrary", new Guid(Endorsement9.Attributes["lux_endorsementlibraryid"].ToString()));
                                        ent["lux_application"] = new EntityReference("lux_propertyownersapplications", appln.Id);
                                        service.Create(ent);
                                    }
                                }
                                //else
                                //{
                                //    if (service.RetrieveMultiple(new FetchExpression(fetch1)).Entities.Count > 0)
                                //    {
                                //        service.Delete("lux_applicationendorsements", service.RetrieveMultiple(new FetchExpression(fetch1)).Entities[0].Id);
                                //    }
                                //}
                            }

                            var Endorsement22 = service.RetrieveMultiple(new FetchExpression(endorsementFetch)).Entities.FirstOrDefault(x => x.Attributes["lux_name"].ToString() == "Electrical Inspection and Testing Programme");
                            if (Endorsement22 != null)
                            {
                                var fetch1 = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                                                              <entity name='lux_applicationendorsements'>
                                                                <attribute name='lux_applicationendorsementsid' />
                                                                <attribute name='lux_name' />
                                                                <attribute name='lux_endorsementnumber' />
                                                                <attribute name='createdon' />
                                                                <order attribute='lux_name' descending='false' />
                                                                <filter type='and'>
                                                                  <condition attribute='statecode' operator='eq' value='0' />
                                                                  <condition attribute='lux_endorsementlibrary' operator='eq' uiname='' uitype='lux_endorsementlibrary' value='{Endorsement22.Attributes["lux_endorsementlibraryid"]}' />
                                                                  <condition attribute='lux_application' operator='eq' uiname='' uitype='lux_propertyownersapplications' value='{appln.Id}' />
                                                                </filter>
                                                              </entity>
                                                            </fetch>";

                                if (service.RetrieveMultiple(new FetchExpression(fetch1)).Entities.Count == 0)
                                {
                                    Entity ent = new Entity("lux_applicationendorsements");
                                    ent["lux_application"] = new EntityReference("lux_propertyownersapplications", appln.Id);
                                    ent["lux_isdefault"] = true;
                                    ent["lux_endorsementnumber"] = Endorsement22.Attributes["new_endorsementnumber"];
                                    ent["lux_endorsementhtml"] = Endorsement22.Attributes["lux_endorsementhtml"];
                                    ent["lux_name"] = Endorsement22.Attributes["lux_name"];
                                    ent["lux_endorsementlibrary"] = new EntityReference("lux_endorsementlibrary", new Guid(Endorsement22.Attributes["lux_endorsementlibraryid"].ToString()));
                                    service.Create(ent);
                                }
                            }

                            var Endorsement23 = service.RetrieveMultiple(new FetchExpression(endorsementFetch)).Entities.FirstOrDefault(x => x.Attributes["lux_name"].ToString() == "Flammables Storage Condition");
                            if (Endorsement23 != null)
                            {
                                var fetch1 = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                                                          <entity name='lux_applicationendorsements'>
                                                            <attribute name='lux_applicationendorsementsid' />
                                                            <attribute name='lux_name' />
                                                            <attribute name='lux_endorsementnumber' />
                                                            <attribute name='createdon' />
                                                            <order attribute='lux_name' descending='false' />
                                                            <filter type='and'>
                                                              <condition attribute='statecode' operator='eq' value='0' />
                                                              <condition attribute='lux_endorsementlibrary' operator='eq' uiname='' uitype='lux_endorsementlibrary' value='{Endorsement23.Attributes["lux_endorsementlibraryid"]}' />
                                                              <condition attribute='lux_application' operator='eq' uiname='' uitype='lux_propertyownersapplications' value='{appln.Id}' />
                                                            </filter>
                                                          </entity>
                                                        </fetch>";

                                if (service.RetrieveMultiple(new FetchExpression(fetch1)).Entities.Count == 0)
                                {
                                    Entity ent = new Entity("lux_applicationendorsements");
                                    ent["lux_application"] = new EntityReference("lux_propertyownersapplications", appln.Id);
                                    ent["lux_isdefault"] = true;
                                    ent["lux_endorsementnumber"] = Endorsement23.Attributes["new_endorsementnumber"];
                                    ent["lux_endorsementhtml"] = Endorsement23.Attributes["lux_endorsementhtml"];
                                    ent["lux_name"] = Endorsement23.Attributes["lux_name"];
                                    ent["lux_endorsementlibrary"] = new EntityReference("lux_endorsementlibrary", new Guid(Endorsement23.Attributes["lux_endorsementlibraryid"].ToString()));
                                    service.Create(ent);
                                }
                            }

                            var Endorsement24 = service.RetrieveMultiple(new FetchExpression(endorsementFetch)).Entities.FirstOrDefault(x => x.Attributes["lux_name"].ToString() == "Portable Heater Exclusion" && x.Attributes["new_endorsementnumber"].ToString() == "MD066");
                            if (Endorsement24 != null)
                            {
                                var fetch1 = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                                                          <entity name='lux_applicationendorsements'>
                                                            <attribute name='lux_applicationendorsementsid' />
                                                            <attribute name='lux_name' />
                                                            <attribute name='lux_endorsementnumber' />
                                                            <attribute name='createdon' />
                                                            <order attribute='lux_name' descending='false' />
                                                            <filter type='and'>
                                                              <condition attribute='statecode' operator='eq' value='0' />
                                                              <condition attribute='lux_endorsementlibrary' operator='eq' uiname='' uitype='lux_endorsementlibrary' value='{Endorsement24.Attributes["lux_endorsementlibraryid"]}' />
                                                              <condition attribute='lux_application' operator='eq' uiname='' uitype='lux_propertyownersapplications' value='{appln.Id}' />
                                                            </filter>
                                                          </entity>
                                                        </fetch>";

                                if (service.RetrieveMultiple(new FetchExpression(fetch1)).Entities.Count == 0)
                                {
                                    Entity ent = new Entity("lux_applicationendorsements");
                                    ent["lux_application"] = new EntityReference("lux_propertyownersapplications", appln.Id);
                                    ent["lux_isdefault"] = true;
                                    ent["lux_endorsementnumber"] = Endorsement24.Attributes["new_endorsementnumber"];
                                    ent["lux_endorsementhtml"] = Endorsement24.Attributes["lux_endorsementhtml"];
                                    ent["lux_name"] = Endorsement24.Attributes["lux_name"];
                                    ent["lux_endorsementlibrary"] = new EntityReference("lux_endorsementlibrary", new Guid(Endorsement24.Attributes["lux_endorsementlibraryid"].ToString()));
                                    service.Create(ent);
                                }
                            }

                            var Endorsement25 = service.RetrieveMultiple(new FetchExpression(endorsementFetch)).Entities.FirstOrDefault(x => x.Attributes["lux_name"].ToString() == "No Smoking Condition");
                            if (Endorsement25 != null)
                            {
                                var fetch1 = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                                                              <entity name='lux_applicationendorsements'>
                                                                <attribute name='lux_applicationendorsementsid' />
                                                                <attribute name='lux_name' />
                                                                <attribute name='lux_endorsementnumber' />
                                                                <attribute name='createdon' />
                                                                <order attribute='lux_name' descending='false' />
                                                                <filter type='and'>
                                                                  <condition attribute='statecode' operator='eq' value='0' />
                                                                  <condition attribute='lux_endorsementlibrary' operator='eq' uiname='' uitype='lux_endorsementlibrary' value='{Endorsement25.Attributes["lux_endorsementlibraryid"]}' />
                                                                  <condition attribute='lux_application' operator='eq' uiname='' uitype='lux_propertyownersapplications' value='{appln.Id}' />
                                                                </filter>
                                                              </entity>
                                                            </fetch>";

                                if (service.RetrieveMultiple(new FetchExpression(fetch1)).Entities.Count == 0)
                                {
                                    Entity ent = new Entity("lux_applicationendorsements");
                                    ent["lux_application"] = new EntityReference("lux_propertyownersapplications", appln.Id);
                                    ent["lux_isdefault"] = true;
                                    ent["lux_endorsementnumber"] = Endorsement25.Attributes["new_endorsementnumber"];
                                    ent["lux_endorsementhtml"] = Endorsement25.Attributes["lux_endorsementhtml"];
                                    ent["lux_name"] = Endorsement25.Attributes["lux_name"];
                                    ent["lux_endorsementlibrary"] = new EntityReference("lux_endorsementlibrary", new Guid(Endorsement25.Attributes["lux_endorsementlibraryid"].ToString()));
                                    service.Create(ent);
                                }
                            }

                            var Endorsement26 = service.RetrieveMultiple(new FetchExpression(endorsementFetch)).Entities.FirstOrDefault(x => x.Attributes["lux_name"].ToString() == "Fork Lift Truck Overnight Charging Condition");
                            if (Endorsement26 != null)
                            {
                                var fetch1 = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                                                          <entity name='lux_applicationendorsements'>
                                                            <attribute name='lux_applicationendorsementsid' />
                                                            <attribute name='lux_name' />
                                                            <attribute name='lux_endorsementnumber' />
                                                            <attribute name='createdon' />
                                                            <order attribute='lux_name' descending='false' />
                                                            <filter type='and'>
                                                              <condition attribute='statecode' operator='eq' value='0' />
                                                              <condition attribute='lux_endorsementlibrary' operator='eq' uiname='' uitype='lux_endorsementlibrary' value='{Endorsement26.Attributes["lux_endorsementlibraryid"]}' />
                                                              <condition attribute='lux_application' operator='eq' uiname='' uitype='lux_propertyownersapplications' value='{appln.Id}' />
                                                            </filter>
                                                          </entity>
                                                        </fetch>";

                                if (service.RetrieveMultiple(new FetchExpression(fetch1)).Entities.Count == 0)
                                {
                                    Entity ent = new Entity("lux_applicationendorsements");
                                    ent["lux_application"] = new EntityReference("lux_propertyownersapplications", appln.Id);
                                    ent["lux_isdefault"] = true;
                                    ent["lux_endorsementnumber"] = Endorsement26.Attributes["new_endorsementnumber"];
                                    ent["lux_endorsementhtml"] = Endorsement26.Attributes["lux_endorsementhtml"];
                                    ent["lux_name"] = Endorsement26.Attributes["lux_name"];
                                    ent["lux_endorsementlibrary"] = new EntityReference("lux_endorsementlibrary", new Guid(Endorsement26.Attributes["lux_endorsementlibraryid"].ToString()));
                                    service.Create(ent);
                                }
                            }

                            var Endorsement27 = service.RetrieveMultiple(new FetchExpression(endorsementFetch)).Entities.FirstOrDefault(x => x.Attributes["lux_name"].ToString() == "Removal of Waste");
                            if (Endorsement27 != null)
                            {
                                var fetch1 = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                                                          <entity name='lux_applicationendorsements'>
                                                            <attribute name='lux_applicationendorsementsid' />
                                                            <attribute name='lux_name' />
                                                            <attribute name='lux_endorsementnumber' />
                                                            <attribute name='createdon' />
                                                            <order attribute='lux_name' descending='false' />
                                                            <filter type='and'>
                                                              <condition attribute='statecode' operator='eq' value='0' />
                                                              <condition attribute='lux_endorsementlibrary' operator='eq' uiname='' uitype='lux_endorsementlibrary' value='{Endorsement27.Attributes["lux_endorsementlibraryid"]}' />
                                                              <condition attribute='lux_application' operator='eq' uiname='' uitype='lux_propertyownersapplications' value='{appln.Id}' />
                                                            </filter>
                                                          </entity>
                                                        </fetch>";

                                if (service.RetrieveMultiple(new FetchExpression(fetch1)).Entities.Count == 0)
                                {
                                    Entity ent = new Entity("lux_applicationendorsements");
                                    ent["lux_application"] = new EntityReference("lux_propertyownersapplications", appln.Id);
                                    ent["lux_isdefault"] = true;
                                    ent["lux_endorsementnumber"] = Endorsement27.Attributes["new_endorsementnumber"];
                                    ent["lux_endorsementhtml"] = Endorsement27.Attributes["lux_endorsementhtml"];
                                    ent["lux_name"] = Endorsement27.Attributes["lux_name"];
                                    ent["lux_endorsementlibrary"] = new EntityReference("lux_endorsementlibrary", new Guid(Endorsement27.Attributes["lux_endorsementlibraryid"].ToString()));
                                    service.Create(ent);
                                }
                            }

                            var Endorsement28 = service.RetrieveMultiple(new FetchExpression(endorsementFetch)).Entities.FirstOrDefault(x => x.Attributes["lux_name"].ToString() == "Stillage Condition");
                            if (Endorsement28 != null)
                            {
                                var fetch1 = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                                                          <entity name='lux_applicationendorsements'>
                                                            <attribute name='lux_applicationendorsementsid' />
                                                            <attribute name='lux_name' />
                                                            <attribute name='lux_endorsementnumber' />
                                                            <attribute name='createdon' />
                                                            <order attribute='lux_name' descending='false' />
                                                            <filter type='and'>
                                                              <condition attribute='statecode' operator='eq' value='0' />
                                                              <condition attribute='lux_endorsementlibrary' operator='eq' uiname='' uitype='lux_endorsementlibrary' value='{Endorsement22.Attributes["lux_endorsementlibraryid"]}' />
                                                              <condition attribute='lux_application' operator='eq' uiname='' uitype='lux_propertyownersapplications' value='{appln.Id}' />
                                                            </filter>
                                                          </entity>
                                                        </fetch>";

                                if (service.RetrieveMultiple(new FetchExpression(fetch1)).Entities.Count == 0)
                                {
                                    Entity ent = new Entity("lux_applicationendorsements");
                                    ent["lux_application"] = new EntityReference("lux_propertyownersapplications", appln.Id);
                                    ent["lux_isdefault"] = true;
                                    ent["lux_endorsementnumber"] = Endorsement28.Attributes["new_endorsementnumber"];
                                    ent["lux_endorsementhtml"] = Endorsement28.Attributes["lux_endorsementhtml"];
                                    ent["lux_name"] = Endorsement28.Attributes["lux_name"];
                                    ent["lux_endorsementlibrary"] = new EntityReference("lux_endorsementlibrary", new Guid(Endorsement28.Attributes["lux_endorsementlibraryid"].ToString()));
                                    service.Create(ent);
                                }
                            }

                            var Endorsement29 = service.RetrieveMultiple(new FetchExpression(endorsementFetch)).Entities.FirstOrDefault(x => x.Attributes["lux_name"].ToString() == "Stacking Condition");
                            if (Endorsement29 != null)
                            {
                                var fetch1 = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                                                          <entity name='lux_applicationendorsements'>
                                                            <attribute name='lux_applicationendorsementsid' />
                                                            <attribute name='lux_name' />
                                                            <attribute name='lux_endorsementnumber' />
                                                            <attribute name='createdon' />
                                                            <order attribute='lux_name' descending='false' />
                                                            <filter type='and'>
                                                              <condition attribute='statecode' operator='eq' value='0' />
                                                              <condition attribute='lux_endorsementlibrary' operator='eq' uiname='' uitype='lux_endorsementlibrary' value='{Endorsement29.Attributes["lux_endorsementlibraryid"]}' />
                                                              <condition attribute='lux_application' operator='eq' uiname='' uitype='lux_propertyownersapplications' value='{appln.Id}' />
                                                            </filter>
                                                          </entity>
                                                        </fetch>";

                                if (service.RetrieveMultiple(new FetchExpression(fetch1)).Entities.Count == 0)
                                {
                                    Entity ent = new Entity("lux_applicationendorsements");
                                    ent["lux_application"] = new EntityReference("lux_propertyownersapplications", appln.Id);
                                    ent["lux_isdefault"] = true;
                                    ent["lux_endorsementnumber"] = Endorsement29.Attributes["new_endorsementnumber"];
                                    ent["lux_endorsementhtml"] = Endorsement29.Attributes["lux_endorsementhtml"];
                                    ent["lux_name"] = Endorsement29.Attributes["lux_name"];
                                    ent["lux_endorsementlibrary"] = new EntityReference("lux_endorsementlibrary", new Guid(Endorsement29.Attributes["lux_endorsementlibraryid"].ToString()));
                                    service.Create(ent);
                                }
                            }

                            var Endorsement31 = service.RetrieveMultiple(new FetchExpression(endorsementFetch)).Entities.FirstOrDefault(x => x.Attributes["lux_name"].ToString() == "Unattended Machinery Condition");
                            if (Endorsement31 != null)
                            {
                                var IsAnyManufacturing = item.Attributes.Contains("lux_isanymanufacturingprocessoperatingunatten") ? item.GetAttributeValue<bool>("lux_isanymanufacturingprocessoperatingunatten") : true;
                                var fetch1 = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                                                          <entity name='lux_applicationendorsements'>
                                                            <attribute name='lux_applicationendorsementsid' />
                                                            <attribute name='lux_name' />
                                                            <attribute name='lux_endorsementnumber' />
                                                            <attribute name='createdon' />
                                                            <order attribute='lux_name' descending='false' />
                                                            <filter type='and'>
                                                              <condition attribute='statecode' operator='eq' value='0' />
                                                              <condition attribute='lux_endorsementlibrary' operator='eq' uiname='' uitype='lux_endorsementlibrary' value='{Endorsement31.Attributes["lux_endorsementlibraryid"]}' />
                                                              <condition attribute='lux_application' operator='eq' uiname='' uitype='lux_propertyownersapplications' value='{appln.Id}' />
                                                            </filter>
                                                          </entity>
                                                        </fetch>";

                                if (IsAnyManufacturing == false)
                                {
                                    if (service.RetrieveMultiple(new FetchExpression(fetch1)).Entities.Count == 0)
                                    {
                                        Entity ent = new Entity("lux_applicationendorsements");
                                        ent["lux_application"] = new EntityReference("lux_propertyownersapplications", appln.Id);
                                        ent["lux_isdefault"] = true;
                                        ent["lux_endorsementnumber"] = Endorsement31.Attributes["new_endorsementnumber"];
                                        ent["lux_endorsementhtml"] = Endorsement31.Attributes.Contains("lux_endorsementhtml") ? Endorsement31.Attributes["lux_endorsementhtml"] : "";
                                        ent["lux_name"] = Endorsement31.Attributes["lux_name"];
                                        ent["lux_endorsementlibrary"] = new EntityReference("lux_endorsementlibrary", new Guid(Endorsement31.Attributes["lux_endorsementlibraryid"].ToString()));
                                        service.Create(ent);
                                    }
                                }
                                //else
                                //{
                                //    if (service.RetrieveMultiple(new FetchExpression(fetch1)).Entities.Count > 0)
                                //    {
                                //        service.Delete("lux_applicationendorsements", service.RetrieveMultiple(new FetchExpression(fetch1)).Entities[0].Id);
                                //    }
                                //}
                            }

                            var Endorsement32 = service.RetrieveMultiple(new FetchExpression(endorsementFetch)).Entities.FirstOrDefault(x => x.Attributes["lux_name"].ToString() == "Height Limit - 10m" && x.Attributes["new_endorsementnumber"].ToString() == "PL062");
                            if (Endorsement32 != null)
                            {
                                var AnyWorkAbove10M = item.Attributes.Contains("lux_doyouundertakeanyworkabove10minheight") ? item.GetAttributeValue<bool>("lux_doyouundertakeanyworkabove10minheight") : false;
                                var fetch1 = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                                                          <entity name='lux_applicationendorsements'>
                                                            <attribute name='lux_applicationendorsementsid' />
                                                            <attribute name='lux_name' />
                                                            <attribute name='lux_endorsementnumber' />
                                                            <attribute name='createdon' />
                                                            <order attribute='lux_name' descending='false' />
                                                            <filter type='and'>
                                                              <condition attribute='statecode' operator='eq' value='0' />
                                                              <condition attribute='lux_endorsementlibrary' operator='eq' uiname='Wood Burner Condition' uitype='lux_endorsementlibrary' value='{Endorsement32.Attributes["lux_endorsementlibraryid"]}' />
                                                              <condition attribute='lux_application' operator='eq' uiname='' uitype='lux_propertyownersapplications' value='{appln.Id}' />
                                                            </filter>
                                                          </entity>
                                                        </fetch>";

                                if (WorkAway == true && AnyWorkAbove10M == false)
                                {
                                    if (service.RetrieveMultiple(new FetchExpression(fetch1)).Entities.Count == 0)
                                    {
                                        Entity ent = new Entity("lux_applicationendorsements");
                                        ent["lux_isdefault"] = true;
                                        ent["lux_endorsementhtml"] = Endorsement32.Attributes.Contains("lux_endorsementhtml") ? Endorsement32.Attributes["lux_endorsementhtml"] : "";
                                        ent["lux_endorsementnumber"] = Endorsement32.Attributes["new_endorsementnumber"];
                                        ent["lux_name"] = Endorsement32.Attributes["lux_name"];
                                        ent["lux_endorsementlibrary"] = new EntityReference("lux_endorsementlibrary", new Guid(Endorsement32.Attributes["lux_endorsementlibraryid"].ToString()));
                                        ent["lux_application"] = new EntityReference("lux_propertyownersapplications", appln.Id);
                                        service.Create(ent);
                                    }
                                }
                                //else
                                //{
                                //    if (service.RetrieveMultiple(new FetchExpression(fetch1)).Entities.Count > 0)
                                //    {
                                //        service.Delete("lux_applicationendorsements", service.RetrieveMultiple(new FetchExpression(fetch1)).Entities[0].Id);
                                //    }
                                //}
                            }

                            var Endorsement33 = service.RetrieveMultiple(new FetchExpression(endorsementFetch)).Entities.FirstOrDefault(x => x.Attributes["lux_name"].ToString() == "Height Limit - 10m" && x.Attributes["new_endorsementnumber"].ToString() == "EL004");
                            if (Endorsement33 != null)
                            {
                                var AnyWorkAbove10M = item.Attributes.Contains("lux_doyouundertakeanyworkabove10minheight") ? item.GetAttributeValue<bool>("lux_doyouundertakeanyworkabove10minheight") : false;
                                var fetch1 = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                                                          <entity name='lux_applicationendorsements'>
                                                            <attribute name='lux_applicationendorsementsid' />
                                                            <attribute name='lux_name' />
                                                            <attribute name='lux_endorsementnumber' />
                                                            <attribute name='createdon' />
                                                            <order attribute='lux_name' descending='false' />
                                                            <filter type='and'>
                                                              <condition attribute='statecode' operator='eq' value='0' />
                                                              <condition attribute='lux_endorsementlibrary' operator='eq' uiname='Wood Burner Condition' uitype='lux_endorsementlibrary' value='{Endorsement33.Attributes["lux_endorsementlibraryid"]}' />
                                                              <condition attribute='lux_application' operator='eq' uiname='' uitype='lux_propertyownersapplications' value='{appln.Id}' />
                                                            </filter>
                                                          </entity>
                                                        </fetch>";

                                if (WorkAway == true && AnyWorkAbove10M == false)
                                {
                                    if (service.RetrieveMultiple(new FetchExpression(fetch1)).Entities.Count == 0)
                                    {
                                        Entity ent = new Entity("lux_applicationendorsements");
                                        ent["lux_isdefault"] = true;
                                        ent["lux_endorsementhtml"] = Endorsement33.Attributes.Contains("lux_endorsementhtml") ? Endorsement33.Attributes["lux_endorsementhtml"] : "";
                                        ent["lux_endorsementnumber"] = Endorsement33.Attributes["new_endorsementnumber"];
                                        ent["lux_name"] = Endorsement33.Attributes["lux_name"];
                                        ent["lux_endorsementlibrary"] = new EntityReference("lux_endorsementlibrary", new Guid(Endorsement33.Attributes["lux_endorsementlibraryid"].ToString()));
                                        ent["lux_application"] = new EntityReference("lux_propertyownersapplications", appln.Id);
                                        service.Create(ent);
                                    }
                                }
                                //else
                                //{
                                //    if (service.RetrieveMultiple(new FetchExpression(fetch1)).Entities.Count > 0)
                                //    {
                                //        service.Delete("lux_applicationendorsements", service.RetrieveMultiple(new FetchExpression(fetch1)).Entities[0].Id);
                                //    }
                                //}
                            }

                            var Endorsement34 = service.RetrieveMultiple(new FetchExpression(endorsementFetch)).Entities.FirstOrDefault(x => x.Attributes["lux_name"].ToString() == "Depth Work Exclusion" && x.Attributes["new_endorsementnumber"].ToString() == "PL103");
                            if (Endorsement34 != null)
                            {
                                var WorkAtDepth = item.Attributes.Contains("lux_doyouundertakeanyworkatdepth") ? item.GetAttributeValue<bool>("lux_doyouundertakeanyworkatdepth") : false;
                                var fetch1 = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                                                          <entity name='lux_applicationendorsements'>
                                                            <attribute name='lux_applicationendorsementsid' />
                                                            <attribute name='lux_name' />
                                                            <attribute name='lux_endorsementnumber' />
                                                            <attribute name='createdon' />
                                                            <order attribute='lux_name' descending='false' />
                                                            <filter type='and'>
                                                              <condition attribute='statecode' operator='eq' value='0' />
                                                              <condition attribute='lux_endorsementlibrary' operator='eq' uiname='' uitype='lux_endorsementlibrary' value='{Endorsement34.Attributes["lux_endorsementlibraryid"]}' />
                                                              <condition attribute='lux_application' operator='eq' uiname='' uitype='lux_propertyownersapplications' value='{appln.Id}' />
                                                            </filter>
                                                          </entity>
                                                        </fetch>";

                                if (WorkAway == true && WorkAtDepth == false)
                                {
                                    if (service.RetrieveMultiple(new FetchExpression(fetch1)).Entities.Count == 0)
                                    {
                                        Entity ent = new Entity("lux_applicationendorsements");
                                        ent["lux_isdefault"] = true;
                                        ent["lux_endorsementhtml"] = Endorsement34.Attributes.Contains("lux_endorsementhtml") ? Endorsement34.Attributes["lux_endorsementhtml"] : "";
                                        ent["lux_endorsementnumber"] = Endorsement34.Attributes["new_endorsementnumber"];
                                        ent["lux_name"] = Endorsement34.Attributes["lux_name"];
                                        ent["lux_endorsementlibrary"] = new EntityReference("lux_endorsementlibrary", new Guid(Endorsement34.Attributes["lux_endorsementlibraryid"].ToString()));
                                        ent["lux_application"] = new EntityReference("lux_propertyownersapplications", appln.Id);
                                        service.Create(ent);
                                    }
                                }
                                //else
                                //{
                                //    if (service.RetrieveMultiple(new FetchExpression(fetch1)).Entities.Count > 0)
                                //    {
                                //        service.Delete("lux_applicationendorsements", service.RetrieveMultiple(new FetchExpression(fetch1)).Entities[0].Id);
                                //    }
                                //}
                            }

                            var Endorsement35 = service.RetrieveMultiple(new FetchExpression(endorsementFetch)).Entities.FirstOrDefault(x => x.Attributes["lux_name"].ToString() == "Depth Work Exclusion" && x.Attributes["new_endorsementnumber"].ToString() == "EL008");
                            if (Endorsement35 != null)
                            {
                                var WorkAtDepth = item.Attributes.Contains("lux_doyouundertakeanyworkatdepth") ? item.GetAttributeValue<bool>("lux_doyouundertakeanyworkatdepth") : false;
                                var fetch1 = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                                                          <entity name='lux_applicationendorsements'>
                                                            <attribute name='lux_applicationendorsementsid' />
                                                            <attribute name='lux_name' />
                                                            <attribute name='lux_endorsementnumber' />
                                                            <attribute name='createdon' />
                                                            <order attribute='lux_name' descending='false' />
                                                            <filter type='and'>
                                                              <condition attribute='statecode' operator='eq' value='0' />
                                                              <condition attribute='lux_endorsementlibrary' operator='eq' uiname='' uitype='lux_endorsementlibrary' value='{Endorsement35.Attributes["lux_endorsementlibraryid"]}' />
                                                              <condition attribute='lux_application' operator='eq' uiname='' uitype='lux_propertyownersapplications' value='{appln.Id}' />
                                                            </filter>
                                                          </entity>
                                                        </fetch>";

                                if (WorkAway == true && WorkAtDepth == false)
                                {
                                    if (service.RetrieveMultiple(new FetchExpression(fetch1)).Entities.Count == 0)
                                    {
                                        Entity ent = new Entity("lux_applicationendorsements");
                                        ent["lux_isdefault"] = true;
                                        ent["lux_endorsementhtml"] = Endorsement35.Attributes.Contains("lux_endorsementhtml") ? Endorsement35.Attributes["lux_endorsementhtml"] : "";
                                        ent["lux_endorsementnumber"] = Endorsement35.Attributes["new_endorsementnumber"];
                                        ent["lux_name"] = Endorsement35.Attributes["lux_name"];
                                        ent["lux_endorsementlibrary"] = new EntityReference("lux_endorsementlibrary", new Guid(Endorsement35.Attributes["lux_endorsementlibraryid"].ToString()));
                                        ent["lux_application"] = new EntityReference("lux_propertyownersapplications", appln.Id);
                                        service.Create(ent);
                                    }
                                }
                                //else
                                //{
                                //    if (service.RetrieveMultiple(new FetchExpression(fetch1)).Entities.Count > 0)
                                //    {
                                //        service.Delete("lux_applicationendorsements", service.RetrieveMultiple(new FetchExpression(fetch1)).Entities[0].Id);
                                //    }
                                //}
                            }

                            var Endorsement36 = service.RetrieveMultiple(new FetchExpression(endorsementFetch)).Entities.FirstOrDefault(x => x.Attributes["lux_name"].ToString() == "Heat Work Away from the Premises Exclusion");
                            if (Endorsement36 != null)
                            {
                                var fetch1 = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                                                          <entity name='lux_applicationendorsements'>
                                                            <attribute name='lux_applicationendorsementsid' />
                                                            <attribute name='lux_name' />
                                                            <attribute name='lux_endorsementnumber' />
                                                            <attribute name='createdon' />
                                                            <order attribute='lux_name' descending='false' />
                                                            <filter type='and'>
                                                              <condition attribute='statecode' operator='eq' value='0' />
                                                              <condition attribute='lux_endorsementlibrary' operator='eq' uiname='' uitype='lux_endorsementlibrary' value='{Endorsement36.Attributes["lux_endorsementlibraryid"]}' />
                                                              <condition attribute='lux_application' operator='eq' uiname='' uitype='lux_propertyownersapplications' value='{appln.Id}' />
                                                            </filter>
                                                          </entity>
                                                        </fetch>";

                                if (WorkAway == true && AnyHeatWorkAway == false)
                                {
                                    if (service.RetrieveMultiple(new FetchExpression(fetch1)).Entities.Count == 0)
                                    {
                                        Entity ent = new Entity("lux_applicationendorsements");
                                        ent["lux_isdefault"] = true;
                                        ent["lux_endorsementhtml"] = Endorsement36.Attributes.Contains("lux_endorsementhtml") ? Endorsement36.Attributes["lux_endorsementhtml"] : "";
                                        ent["lux_endorsementnumber"] = Endorsement36.Attributes["new_endorsementnumber"];
                                        ent["lux_name"] = Endorsement36.Attributes["lux_name"];
                                        ent["lux_endorsementlibrary"] = new EntityReference("lux_endorsementlibrary", new Guid(Endorsement36.Attributes["lux_endorsementlibraryid"].ToString()));
                                        ent["lux_application"] = new EntityReference("lux_propertyownersapplications", appln.Id);
                                        service.Create(ent);
                                    }
                                }
                                //else
                                //{
                                //    if (service.RetrieveMultiple(new FetchExpression(fetch1)).Entities.Count > 0)
                                //    {
                                //        service.Delete("lux_applicationendorsements", service.RetrieveMultiple(new FetchExpression(fetch1)).Entities[0].Id);
                                //    }
                                //}
                            }
                        }
                    }
                }
            }
            else if (productName == "Pubs & Restaurants")
            {
                var fetch = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                                      <entity name='lux_pubsrestaurantspropertyownersapplicatio'>
                                        <attribute name='lux_pubsrestaurantspropertyownersapplicatioid' />
                                        <attribute name='lux_name' />
                                        <attribute name='createdon' />
                                        <attribute name='lux_rentreceivable' />
                                        <attribute name='lux_icow' />
                                        <attribute name='lux_typeofcover' />
                                        <attribute name='lux_bookdebts' />
                                        <attribute name='lux_issubsidencecoverrequired' />
                                        <attribute name='lux_amount' />
                                        <attribute name='lux_maintradeforthispremises' />
                                        <attribute name='lux_additionalincreasedcostofworking' />
                                        <attribute name='lux_doesthepremiseshaveaflatroof' />
                                        <attribute name='lux_flatroofpercentage' />
                                        <attribute name='lux_doesthepremiseshaveabasement' />
                                        <attribute name='lux_isthereanatm' />     
                                        <attribute name='lux_isthereanintruderalarminstalledandinworki' />                                
                                        <attribute name='lux_whattypeofalarmisinstalled' />
                                        <attribute name='lux_typeofheating' />
                                        <attribute name='lux_isownerresponsibleforfillingandemptyingth' />
                                        <attribute name='lux_isthereanydeepfatfrying' />
                                        <attribute name='lux_floodscore' />
                                        <attribute name='lux_subsidencescore' />
                                        <attribute name='lux_crimescore' />
                                        <attribute name='lux_securityrating' />
                                        <order attribute='lux_name' descending='false' />
                                        <filter type='and'>
                                          <condition attribute='lux_propertyownersapplications' operator='eq' uiname='' uitype='lux_propertyownersapplications' value='{appln.Id}' />
                                        </filter>
                                        <link-entity name='lux_propertyownersapplications' from='lux_propertyownersapplicationsid' to='lux_propertyownersapplications' visible='false' link-type='outer' alias='appln'>
                                          <attribute name='lux_anyheatworkawayundertaken' />
                                          <attribute name='lux_isanyworkawaycarriedoutotherthanforcollec' />
                                        </link-entity>
                                      </entity>
                                    </fetch>";

                if (service.RetrieveMultiple(new FetchExpression(fetch)).Entities.Count > 0)
                {
                    var endorsementFetch = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                                                          <entity name='lux_endorsementlibrary'>
                                                            <attribute name='lux_name' />
                                                            <attribute name='new_product' />
                                                            <attribute name='lux_insurer' />
                                                            <attribute name='lux_endorsementdescription' />
                                                            <attribute name='lux_endorsementhtml' />
                                                            <attribute name='new_endorsementnumber' />
                                                            <attribute name='lux_endorsementlibraryid' />
                                                            <order attribute='lux_name' descending='false' />
                                                            <filter type='and'>
                                                              <condition attribute='statecode' operator='eq' value='0' />
                                                              <condition attribute='lux_name' operator='not-null' />
                                                              <filter type='or'>
                                                                <condition attribute='new_product' operator='eq' uiname='' uitype='product' value='{"0c49880c-aba1-eb11-b1ac-00224840d300"}' />
                                                                <condition attribute='new_product' operator='null' />
                                                              </filter>
                                                            </filter>
                                                          </entity>
                                                        </fetch>";

                    foreach (var item in service.RetrieveMultiple(new FetchExpression(fetch)).Entities)
                    {
                        var FlatRoof = item.Attributes.Contains("lux_doesthepremiseshaveaflatroof") ? item.GetAttributeValue<bool>("lux_doesthepremiseshaveaflatroof") : false;
                        var ATM = item.Attributes.Contains("lux_isthereanatm") ? item.GetAttributeValue<bool>("lux_isthereanatm") : false;
                        var OwnerResponsible = item.Attributes.Contains("lux_isownerresponsibleforfillingandemptyingth") ? item.GetAttributeValue<bool>("lux_isownerresponsibleforfillingandemptyingth") : false;
                        var AnyDFF = item.Attributes.Contains("lux_isthereanydeepfatfrying") ? item.GetAttributeValue<bool>("lux_isthereanydeepfatfrying") : false;
                        var AnyHeatWorkAway = item.Attributes.Contains("appln.lux_anyheatworkawayundertaken") ? ((bool)(item.GetAttributeValue<AliasedValue>("appln.lux_anyheatworkawayundertaken").Value)) : false;
                        var WorkAway = item.Attributes.Contains("appln.lux_isanyworkawaycarriedoutotherthanforcollec") ? ((bool)(item.GetAttributeValue<AliasedValue>("appln.lux_isanyworkawaycarriedoutotherthanforcollec").Value)) : false;
                        var IntruderAlarm = item.Attributes.Contains("lux_isthereanintruderalarminstalledandinworki") ? item.GetAttributeValue<bool>("lux_isthereanintruderalarminstalledandinworki") : false;
                        var TypeOFAlarm = item.Attributes.Contains("lux_whattypeofalarmisinstalled") ? item.GetAttributeValue<OptionSetValue>("lux_whattypeofalarmisinstalled").Value : 0;
                        var productType = appln.Attributes.Contains("lux_pubsrestaurantproducttype") ? appln.GetAttributeValue<OptionSetValue>("lux_pubsrestaurantproducttype").Value : 0;
                        var Basement = item.Attributes.Contains("lux_doesthepremiseshaveabasement") ? item.GetAttributeValue<bool>("lux_doesthepremiseshaveabasement") : false;

                        if (service.RetrieveMultiple(new FetchExpression(endorsementFetch)).Entities.Count > 0)
                        {
                            var Endorsement1 = new Entity();
                            if (OwnerResponsible == true)
                            {
                                Endorsement1 = service.RetrieveMultiple(new FetchExpression(endorsementFetch)).Entities.FirstOrDefault(x => x.Attributes["lux_name"].ToString() == "Automated Teller Machine (ATM) - Self Fill");
                            }
                            else
                            {
                                Endorsement1 = service.RetrieveMultiple(new FetchExpression(endorsementFetch)).Entities.FirstOrDefault(x => x.Attributes["lux_name"].ToString() == "Automated Teller Machine (ATM) - Third Party Provided & Maintained Machines");
                            }

                            if (Endorsement1 != null)
                            {
                                var fetch1 = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                                                          <entity name='lux_applicationendorsements'>
                                                            <attribute name='lux_applicationendorsementsid' />
                                                            <attribute name='lux_name' />
                                                            <attribute name='lux_endorsementnumber' />
                                                            <attribute name='createdon' />
                                                            <order attribute='lux_name' descending='false' />
                                                            <filter type='and'>
                                                              <condition attribute='statecode' operator='eq' value='0' />
                                                              <condition attribute='lux_endorsementlibrary' operator='eq' uiname='' uitype='lux_endorsementlibrary' value='{Endorsement1.Attributes["lux_endorsementlibraryid"]}' />
                                                              <condition attribute='lux_application' operator='eq' uiname='' uitype='lux_propertyownersapplications' value='{appln.Id}' />
                                                            </filter>
                                                          </entity>
                                                        </fetch>";

                                if (ATM == true) // Flat roof
                                {
                                    if (service.RetrieveMultiple(new FetchExpression(fetch1)).Entities.Count == 0)
                                    {
                                        Entity ent = new Entity("lux_applicationendorsements");
                                        ent["lux_isdefault"] = true;
                                        ent["lux_endorsementhtml"] = Endorsement1.Attributes.Contains("lux_endorsementhtml") ? Endorsement1.Attributes["lux_endorsementhtml"] : "";
                                        ent["lux_endorsementnumber"] = Endorsement1.Attributes["new_endorsementnumber"];
                                        ent["lux_name"] = Endorsement1.Attributes["lux_name"];
                                        ent["lux_endorsementlibrary"] = new EntityReference("lux_endorsementlibrary", new Guid(Endorsement1.Attributes["lux_endorsementlibraryid"].ToString()));
                                        ent["lux_application"] = new EntityReference("lux_propertyownersapplications", appln.Id);
                                        service.Create(ent);
                                    }
                                }
                                //else
                                //{
                                //    if (service.RetrieveMultiple(new FetchExpression(fetch1)).Entities.Count > 0)
                                //    {
                                //        service.Delete("lux_applicationendorsements", service.RetrieveMultiple(new FetchExpression(fetch1)).Entities[0].Id);
                                //    }
                                //}
                            }

                            if (productType != 972970002)
                            {
                                var Endorsement2 = service.RetrieveMultiple(new FetchExpression(endorsementFetch)).Entities.FirstOrDefault(x => x.Attributes["lux_name"].ToString() == "Kitchen Precautions");
                                if (Endorsement2 != null)
                                {
                                    var fetch1 = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                                                          <entity name='lux_applicationendorsements'>
                                                            <attribute name='lux_applicationendorsementsid' />
                                                            <attribute name='lux_name' />
                                                            <attribute name='lux_endorsementnumber' />
                                                            <attribute name='createdon' />
                                                            <order attribute='lux_name' descending='false' />
                                                            <filter type='and'>
                                                              <condition attribute='statecode' operator='eq' value='0' />
                                                              <condition attribute='lux_endorsementlibrary' operator='eq' uiname='' uitype='lux_endorsementlibrary' value='{Endorsement2.Attributes["lux_endorsementlibraryid"]}' />
                                                              <condition attribute='lux_application' operator='eq' uiname='' uitype='lux_propertyownersapplications' value='{appln.Id}' />
                                                            </filter>
                                                          </entity>
                                                        </fetch>";

                                    if (AnyDFF == true) // Flat roof
                                    {
                                        if (service.RetrieveMultiple(new FetchExpression(fetch1)).Entities.Count == 0)
                                        {
                                            Entity ent = new Entity("lux_applicationendorsements");
                                            ent["lux_isdefault"] = true;
                                            ent["lux_endorsementhtml"] = Endorsement2.Attributes.Contains("lux_endorsementhtml") ? Endorsement2.Attributes["lux_endorsementhtml"] : "";
                                            ent["lux_endorsementnumber"] = Endorsement2.Attributes["new_endorsementnumber"];
                                            ent["lux_name"] = Endorsement2.Attributes["lux_name"];
                                            ent["lux_endorsementlibrary"] = new EntityReference("lux_endorsementlibrary", new Guid(Endorsement2.Attributes["lux_endorsementlibraryid"].ToString()));
                                            ent["lux_application"] = new EntityReference("lux_propertyownersapplications", appln.Id);
                                            service.Create(ent);
                                        }
                                    }
                                    //else
                                    //{
                                    //    if (service.RetrieveMultiple(new FetchExpression(fetch1)).Entities.Count > 0)
                                    //    {
                                    //        service.Delete("lux_applicationendorsements", service.RetrieveMultiple(new FetchExpression(fetch1)).Entities[0].Id);
                                    //    }
                                    //}
                                }
                            }

                            var Endorsement3 = service.RetrieveMultiple(new FetchExpression(endorsementFetch)).Entities.FirstOrDefault(x => x.Attributes["lux_name"].ToString() == "Roof Maintenance Condition");
                            if (Endorsement3 != null)
                            {
                                var fetch1 = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                                                          <entity name='lux_applicationendorsements'>
                                                            <attribute name='lux_applicationendorsementsid' />
                                                            <attribute name='lux_name' />
                                                            <attribute name='lux_endorsementnumber' />
                                                            <attribute name='createdon' />
                                                            <order attribute='lux_name' descending='false' />
                                                            <filter type='and'>
                                                              <condition attribute='statecode' operator='eq' value='0' />
                                                              <condition attribute='lux_endorsementlibrary' operator='eq' uiname='' uitype='lux_endorsementlibrary' value='{Endorsement3.Attributes["lux_endorsementlibraryid"]}' />
                                                              <condition attribute='lux_application' operator='eq' uiname='' uitype='lux_propertyownersapplications' value='{appln.Id}' />
                                                            </filter>
                                                          </entity>
                                                        </fetch>";

                                if (FlatRoof == true) // Flat roof
                                {
                                    if (service.RetrieveMultiple(new FetchExpression(fetch1)).Entities.Count == 0)
                                    {
                                        Entity ent = new Entity("lux_applicationendorsements");
                                        ent["lux_isdefault"] = true;
                                        ent["lux_endorsementhtml"] = Endorsement3.Attributes.Contains("lux_endorsementhtml") ? Endorsement3.Attributes["lux_endorsementhtml"] : "";
                                        ent["lux_endorsementnumber"] = Endorsement3.Attributes["new_endorsementnumber"];
                                        ent["lux_name"] = Endorsement3.Attributes["lux_name"];
                                        ent["lux_endorsementlibrary"] = new EntityReference("lux_endorsementlibrary", new Guid(Endorsement3.Attributes["lux_endorsementlibraryid"].ToString()));
                                        ent["lux_application"] = new EntityReference("lux_propertyownersapplications", appln.Id);
                                        service.Create(ent);
                                    }
                                }
                                //else
                                //{
                                //    if (service.RetrieveMultiple(new FetchExpression(fetch1)).Entities.Count > 0)
                                //    {
                                //        service.Delete("lux_applicationendorsements", service.RetrieveMultiple(new FetchExpression(fetch1)).Entities[0].Id);
                                //    }
                                //}
                            }

                            var Endorsement4 = service.RetrieveMultiple(new FetchExpression(endorsementFetch)).Entities.FirstOrDefault(x => x.Attributes["lux_name"].ToString() == "Heat Application Condition");
                            if (Endorsement4 != null)
                            {
                                var fetch1 = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                                                          <entity name='lux_applicationendorsements'>
                                                            <attribute name='lux_applicationendorsementsid' />
                                                            <attribute name='lux_name' />
                                                            <attribute name='lux_endorsementnumber' />
                                                            <attribute name='createdon' />
                                                            <order attribute='lux_name' descending='false' />
                                                            <filter type='and'>
                                                              <condition attribute='statecode' operator='eq' value='0' />
                                                              <condition attribute='lux_endorsementlibrary' operator='eq' uiname='' uitype='lux_endorsementlibrary' value='{Endorsement4.Attributes["lux_endorsementlibraryid"]}' />
                                                              <condition attribute='lux_application' operator='eq' uiname='' uitype='lux_propertyownersapplications' value='{appln.Id}' />
                                                            </filter>
                                                          </entity>
                                                        </fetch>";

                                if (AnyHeatWorkAway == true)
                                {
                                    if (service.RetrieveMultiple(new FetchExpression(fetch1)).Entities.Count == 0)
                                    {
                                        Entity ent = new Entity("lux_applicationendorsements");
                                        ent["lux_isdefault"] = true;
                                        ent["lux_endorsementhtml"] = Endorsement4.Attributes.Contains("lux_endorsementhtml") ? Endorsement4.Attributes["lux_endorsementhtml"] : "";
                                        ent["lux_endorsementnumber"] = Endorsement4.Attributes["new_endorsementnumber"];
                                        ent["lux_name"] = Endorsement4.Attributes["lux_name"];
                                        ent["lux_endorsementlibrary"] = new EntityReference("lux_endorsementlibrary", new Guid(Endorsement4.Attributes["lux_endorsementlibraryid"].ToString()));
                                        ent["lux_application"] = new EntityReference("lux_propertyownersapplications", appln.Id);
                                        service.Create(ent);
                                    }
                                }
                                //else
                                //{
                                //    if (service.RetrieveMultiple(new FetchExpression(fetch1)).Entities.Count > 0)
                                //    {
                                //        service.Delete("lux_applicationendorsements", service.RetrieveMultiple(new FetchExpression(fetch1)).Entities[0].Id);
                                //    }
                                //}
                            }

                            var FloodScore = item.Attributes.Contains("lux_floodscore") ? item.GetAttributeValue<int>("lux_floodscore") : 0;
                            var SubsidienceScore = item.Attributes.Contains("lux_subsidencescore") ? item.GetAttributeValue<int>("lux_subsidencescore") : 0;

                            var Endorsement5 = service.RetrieveMultiple(new FetchExpression(endorsementFetch)).Entities.FirstOrDefault(x => x.Attributes["lux_name"].ToString() == "Increased Flood Excess - £1,000");
                            if (Endorsement5 != null)
                            {
                                var fetch1 = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                                                          <entity name='lux_applicationendorsements'>
                                                            <attribute name='lux_applicationendorsementsid' />
                                                            <attribute name='lux_name' />
                                                            <attribute name='lux_endorsementnumber' />
                                                            <attribute name='createdon' />
                                                            <order attribute='lux_name' descending='false' />
                                                            <filter type='and'>
                                                              <condition attribute='statecode' operator='eq' value='0' />
                                                              <condition attribute='lux_endorsementlibrary' operator='eq' uiname='' uitype='lux_endorsementlibrary' value='{Endorsement5.Attributes["lux_endorsementlibraryid"]}' />
                                                              <condition attribute='lux_application' operator='eq' uiname='' uitype='lux_propertyownersapplications' value='{appln.Id}' />
                                                            </filter>
                                                          </entity>
                                                        </fetch>";

                                //if (model.IsLive == 0)
                                //{
                                if (FloodScore == 10 || Basement == true) //£1000 Flood Excess
                                {
                                    if (service.RetrieveMultiple(new FetchExpression(fetch1)).Entities.Count == 0)
                                    {
                                        Entity ent = new Entity("lux_applicationendorsements");
                                        ent["lux_isdefault"] = true;
                                        ent["lux_endorsementnumber"] = Endorsement5.Attributes["new_endorsementnumber"];
                                        ent["lux_endorsementhtml"] = Endorsement5.Attributes["lux_endorsementhtml"];
                                        ent["lux_name"] = Endorsement5.Attributes["lux_name"];
                                        ent["lux_endorsementlibrary"] = new EntityReference("lux_endorsementlibrary", new Guid(Endorsement5.Attributes["lux_endorsementlibraryid"].ToString()));
                                        ent["lux_application"] = new EntityReference("lux_propertyownersapplications", appln.Id);
                                        service.Create(ent);
                                    }
                                }
                                //else
                                //{
                                //    if (service.RetrieveMultiple(new FetchExpression(fetch1)).Entities.Count > 0)
                                //    {
                                //        service.Delete("lux_applicationendorsements", service.RetrieveMultiple(new FetchExpression(fetch1)).Entities[0].Id);
                                //    }
                                //}
                                //}
                                //else
                                //{
                                //    if (FloodScore == 10) //£1000 Flood Excess
                                //    {
                                //        if (service.RetrieveMultiple(new FetchExpression(fetch1)).Entities.Count == 0)
                                //        {
                                //            Entity ent = new Entity("lux_applicationendorsements");
                                //            ent["lux_isdefault"] = true;
                                //            ent["lux_endorsementnumber"] = Endorsement5.Attributes["new_endorsementnumber"];
                                //            ent["lux_endorsementhtml"] = Endorsement5.Attributes["lux_endorsementhtml"];
                                //            ent["lux_name"] = Endorsement5.Attributes["lux_name"];
                                //            ent["lux_endorsementlibrary"] = new EntityReference("lux_endorsementlibrary", new Guid(Endorsement5.Attributes["lux_endorsementlibraryid"].ToString()));
                                //            ent["lux_application"] = new EntityReference("lux_propertyownersapplications", appln.Id);
                                //            service.Create(ent);
                                //        }
                                //    }
                                //    //else
                                //    //{
                                //    //    if (service.RetrieveMultiple(new FetchExpression(fetch1)).Entities.Count > 0)
                                //    //    {
                                //    //        service.Delete("lux_applicationendorsements", service.RetrieveMultiple(new FetchExpression(fetch1)).Entities[0].Id);
                                //    //    }
                                //    //}
                                //}
                            }

                            var Endorsement6 = service.RetrieveMultiple(new FetchExpression(endorsementFetch)).Entities.FirstOrDefault(x => x.Attributes["lux_name"].ToString() == "Flood Exclusion");
                            if (Endorsement6 != null)
                            {
                                var fetch1 = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                                                          <entity name='lux_applicationendorsements'>
                                                            <attribute name='lux_applicationendorsementsid' />
                                                            <attribute name='lux_name' />
                                                            <attribute name='lux_endorsementnumber' />
                                                            <attribute name='createdon' />
                                                            <order attribute='lux_name' descending='false' />
                                                            <filter type='and'>
                                                              <condition attribute='statecode' operator='eq' value='0' />
                                                              <condition attribute='lux_endorsementlibrary' operator='eq' uiname='' uitype='lux_endorsementlibrary' value='{Endorsement6.Attributes["lux_endorsementlibraryid"]}' />
                                                              <condition attribute='lux_application' operator='eq' uiname='' uitype='lux_propertyownersapplications' value='{appln.Id}' />
                                                            </filter>
                                                          </entity>
                                                        </fetch>";
                                if (FloodScore >= 11) //Decline - Exclude Flood Cover
                                {
                                    if (service.RetrieveMultiple(new FetchExpression(fetch1)).Entities.Count == 0)
                                    {
                                        Entity ent = new Entity("lux_applicationendorsements");
                                        ent["lux_isdefault"] = true;
                                        ent["lux_endorsementnumber"] = Endorsement6.Attributes["new_endorsementnumber"];
                                        ent["lux_endorsementhtml"] = Endorsement6.Attributes["lux_endorsementhtml"];
                                        ent["lux_name"] = Endorsement6.Attributes["lux_name"];
                                        ent["lux_endorsementlibrary"] = new EntityReference("lux_endorsementlibrary", new Guid(Endorsement6.Attributes["lux_endorsementlibraryid"].ToString()));
                                        ent["lux_application"] = new EntityReference("lux_propertyownersapplications", appln.Id);
                                        service.Create(ent);
                                    }
                                }
                                //else
                                //{
                                //    if (service.RetrieveMultiple(new FetchExpression(fetch1)).Entities.Count > 0)
                                //    {
                                //        service.Delete("lux_applicationendorsements", service.RetrieveMultiple(new FetchExpression(fetch1)).Entities[0].Id);
                                //    }
                                //}
                            }

                            //if (model.IsLive == 1)
                            //{
                            //    var Endorsement7 = service.RetrieveMultiple(new FetchExpression(endorsementFetch)).Entities.FirstOrDefault(x => x.Attributes["lux_name"].ToString() == "Increased Subsidence Excess - £2,500");
                            //    if (Endorsement7 != null)
                            //    {
                            //        var fetch1 = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                            //                          <entity name='lux_applicationendorsements'>
                            //                            <attribute name='lux_applicationendorsementsid' />
                            //                            <attribute name='lux_name' />
                            //                            <attribute name='lux_endorsementnumber' />
                            //                            <attribute name='createdon' />
                            //                            <order attribute='lux_name' descending='false' />
                            //                            <filter type='and'>
                            //                              <condition attribute='statecode' operator='eq' value='0' />
                            //                              <condition attribute='lux_endorsementlibrary' operator='eq' uiname='' uitype='lux_endorsementlibrary' value='{Endorsement7.Attributes["lux_endorsementlibraryid"]}' />
                            //                              <condition attribute='lux_application' operator='eq' uiname='' uitype='lux_propertyownersapplications' value='{appln.Id}' />
                            //                            </filter>
                            //                          </entity>
                            //                        </fetch>";
                            //        if ((SubsidienceScore == 4 || SubsidienceScore == 5) && item.GetAttributeValue<bool>("lux_issubsidencecoverrequired") == true) //£750 Flood Excess
                            //        {
                            //            if (service.RetrieveMultiple(new FetchExpression(fetch1)).Entities.Count == 0)
                            //            {
                            //                Entity ent = new Entity("lux_applicationendorsements");
                            //                ent["lux_isdefault"] = true;
                            //                ent["lux_endorsementnumber"] = Endorsement7.Attributes["new_endorsementnumber"];
                            //                ent["lux_endorsementhtml"] = Endorsement7.Attributes["lux_endorsementhtml"];
                            //                ent["lux_name"] = Endorsement7.Attributes["lux_name"];
                            //                ent["lux_endorsementlibrary"] = new EntityReference("lux_endorsementlibrary", new Guid(Endorsement7.Attributes["lux_endorsementlibraryid"].ToString()));
                            //                ent["lux_application"] = new EntityReference("lux_propertyownersapplications", appln.Id);
                            //                service.Create(ent);
                            //            }
                            //        }
                            //        //else
                            //        //{
                            //        //    if (service.RetrieveMultiple(new FetchExpression(fetch1)).Entities.Count > 0)
                            //        //    {
                            //        //        service.Delete("lux_applicationendorsements", service.RetrieveMultiple(new FetchExpression(fetch1)).Entities[0].Id);
                            //        //    }
                            //        //}
                            //    }

                            //    var Endorsement8 = service.RetrieveMultiple(new FetchExpression(endorsementFetch)).Entities.FirstOrDefault(x => x.Attributes["lux_name"].ToString() == "Increased Subsidence Excess - £5,000");
                            //    if (Endorsement8 != null)
                            //    {
                            //        var fetch1 = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                            //                          <entity name='lux_applicationendorsements'>
                            //                            <attribute name='lux_applicationendorsementsid' />
                            //                            <attribute name='lux_name' />
                            //                            <attribute name='lux_endorsementnumber' />
                            //                            <attribute name='createdon' />
                            //                            <order attribute='lux_name' descending='false' />
                            //                            <filter type='and'>
                            //                              <condition attribute='statecode' operator='eq' value='0' />
                            //                              <condition attribute='lux_endorsementlibrary' operator='eq' uiname='' uitype='lux_endorsementlibrary' value='{Endorsement8.Attributes["lux_endorsementlibraryid"]}' />
                            //                              <condition attribute='lux_application' operator='eq' uiname='' uitype='lux_propertyownersapplications' value='{appln.Id}' />
                            //                            </filter>
                            //                          </entity>
                            //                        </fetch>";
                            //        if (SubsidienceScore == 6 && item.GetAttributeValue<bool>("lux_issubsidencecoverrequired") == true) //£750 Flood Excess
                            //        {
                            //            if (service.RetrieveMultiple(new FetchExpression(fetch1)).Entities.Count == 0)
                            //            {
                            //                Entity ent = new Entity("lux_applicationendorsements");
                            //                ent["lux_isdefault"] = true;
                            //                ent["lux_endorsementnumber"] = Endorsement8.Attributes["new_endorsementnumber"];
                            //                ent["lux_endorsementhtml"] = Endorsement8.Attributes["lux_endorsementhtml"];
                            //                ent["lux_name"] = Endorsement8.Attributes["lux_name"];
                            //                ent["lux_endorsementlibrary"] = new EntityReference("lux_endorsementlibrary", new Guid(Endorsement8.Attributes["lux_endorsementlibraryid"].ToString()));
                            //                ent["lux_application"] = new EntityReference("lux_propertyownersapplications", appln.Id);
                            //                service.Create(ent);
                            //            }
                            //        }
                            //        //else
                            //        //{
                            //        //    if (service.RetrieveMultiple(new FetchExpression(fetch1)).Entities.Count > 0)
                            //        //    {
                            //        //        service.Delete("lux_applicationendorsements", service.RetrieveMultiple(new FetchExpression(fetch1)).Entities[0].Id);
                            //        //    }
                            //        //}
                            //    }
                            //}
                            //else
                            //{
                            var Endorsement7 = service.RetrieveMultiple(new FetchExpression(endorsementFetch)).Entities.FirstOrDefault(x => x.Attributes["lux_name"].ToString() == "Increased Subsidence Excess - £2,500");
                            if (Endorsement7 != null)
                            {
                                var fetch1 = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                                                          <entity name='lux_applicationendorsements'>
                                                            <attribute name='lux_applicationendorsementsid' />
                                                            <attribute name='lux_name' />
                                                            <attribute name='lux_endorsementnumber' />
                                                            <attribute name='createdon' />
                                                            <order attribute='lux_name' descending='false' />
                                                            <filter type='and'>
                                                              <condition attribute='statecode' operator='eq' value='0' />
                                                              <condition attribute='lux_endorsementlibrary' operator='eq' uiname='' uitype='lux_endorsementlibrary' value='{Endorsement7.Attributes["lux_endorsementlibraryid"]}' />
                                                              <condition attribute='lux_application' operator='eq' uiname='' uitype='lux_propertyownersapplications' value='{appln.Id}' />
                                                            </filter>
                                                          </entity>
                                                        </fetch>";
                                if ((SubsidienceScore == 6) && item.GetAttributeValue<bool>("lux_issubsidencecoverrequired") == true) //£750 Flood Excess
                                {
                                    if (service.RetrieveMultiple(new FetchExpression(fetch1)).Entities.Count == 0)
                                    {
                                        Entity ent = new Entity("lux_applicationendorsements");
                                        ent["lux_isdefault"] = true;
                                        ent["lux_endorsementnumber"] = Endorsement7.Attributes["new_endorsementnumber"];
                                        ent["lux_endorsementhtml"] = Endorsement7.Attributes["lux_endorsementhtml"];
                                        ent["lux_name"] = Endorsement7.Attributes["lux_name"];
                                        ent["lux_endorsementlibrary"] = new EntityReference("lux_endorsementlibrary", new Guid(Endorsement7.Attributes["lux_endorsementlibraryid"].ToString()));
                                        ent["lux_application"] = new EntityReference("lux_propertyownersapplications", appln.Id);
                                        service.Create(ent);
                                    }
                                }
                            }
                            //}

                            var Endorsement10 = service.RetrieveMultiple(new FetchExpression(endorsementFetch)).Entities.FirstOrDefault(x => x.Attributes["lux_name"].ToString() == "Subsidence Exclusion");
                            if (Endorsement10 != null)
                            {
                                var fetch1 = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                                                          <entity name='lux_applicationendorsements'>
                                                            <attribute name='lux_applicationendorsementsid' />
                                                            <attribute name='lux_name' />
                                                            <attribute name='lux_endorsementnumber' />
                                                            <attribute name='createdon' />
                                                            <order attribute='lux_name' descending='false' />
                                                            <filter type='and'>
                                                              <condition attribute='statecode' operator='eq' value='0' />
                                                              <condition attribute='lux_endorsementlibrary' operator='eq' uiname='' uitype='lux_endorsementlibrary' value='{Endorsement10.Attributes["lux_endorsementlibraryid"]}' />
                                                              <condition attribute='lux_application' operator='eq' uiname='' uitype='lux_propertyownersapplications' value='{appln.Id}' />
                                                            </filter>
                                                          </entity>
                                                        </fetch>";
                                if (SubsidienceScore == 7 && item.GetAttributeValue<bool>("lux_issubsidencecoverrequired") == true) //£750 Flood Excess
                                {
                                    if (service.RetrieveMultiple(new FetchExpression(fetch1)).Entities.Count == 0)
                                    {
                                        Entity ent = new Entity("lux_applicationendorsements");
                                        ent["lux_isdefault"] = true;
                                        ent["lux_endorsementnumber"] = Endorsement10.Attributes["new_endorsementnumber"];
                                        ent["lux_endorsementhtml"] = Endorsement10.Attributes["lux_endorsementhtml"];
                                        ent["lux_name"] = Endorsement10.Attributes["lux_name"];
                                        ent["lux_endorsementlibrary"] = new EntityReference("lux_endorsementlibrary", new Guid(Endorsement10.Attributes["lux_endorsementlibraryid"].ToString()));
                                        ent["lux_application"] = new EntityReference("lux_propertyownersapplications", appln.Id);
                                        service.Create(ent);
                                    }
                                }
                                //else
                                //{
                                //    if (service.RetrieveMultiple(new FetchExpression(fetch1)).Entities.Count > 0)
                                //    {
                                //        service.Delete("lux_applicationendorsements", service.RetrieveMultiple(new FetchExpression(fetch1)).Entities[0].Id);
                                //    }
                                //}
                            }

                            var Endorsement11 = service.RetrieveMultiple(new FetchExpression(endorsementFetch)).Entities.FirstOrDefault(x => x.Attributes["lux_name"].ToString() == "Increased Flood Excess - £500");
                            if (Endorsement11 != null)
                            {
                                var fetch1 = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                                                          <entity name='lux_applicationendorsements'>
                                                            <attribute name='lux_applicationendorsementsid' />
                                                            <attribute name='lux_name' />
                                                            <attribute name='lux_endorsementnumber' />
                                                            <attribute name='createdon' />
                                                            <order attribute='lux_name' descending='false' />
                                                            <filter type='and'>
                                                              <condition attribute='statecode' operator='eq' value='0' />
                                                              <condition attribute='lux_endorsementlibrary' operator='eq' uiname='' uitype='lux_endorsementlibrary' value='{Endorsement11.Attributes["lux_endorsementlibraryid"]}' />
                                                              <condition attribute='lux_application' operator='eq' uiname='' uitype='lux_propertyownersapplications' value='{appln.Id}' />
                                                            </filter>
                                                          </entity>
                                                        </fetch>";
                                if (FloodScore == 9) //£500 Flood Excess
                                {
                                    if (service.RetrieveMultiple(new FetchExpression(fetch1)).Entities.Count == 0)
                                    {
                                        Entity ent = new Entity("lux_applicationendorsements");
                                        ent["lux_isdefault"] = true;
                                        ent["lux_endorsementnumber"] = Endorsement11.Attributes["new_endorsementnumber"];
                                        ent["lux_endorsementhtml"] = Endorsement11.Attributes["lux_endorsementhtml"];
                                        ent["lux_name"] = Endorsement11.Attributes["lux_name"];
                                        ent["lux_endorsementlibrary"] = new EntityReference("lux_endorsementlibrary", new Guid(Endorsement11.Attributes["lux_endorsementlibraryid"].ToString()));
                                        ent["lux_application"] = new EntityReference("lux_propertyownersapplications", appln.Id);
                                        service.Create(ent);
                                    }
                                }
                                //else
                                //{
                                //    if (service.RetrieveMultiple(new FetchExpression(fetch1)).Entities.Count > 0)
                                //    {
                                //        service.Delete("lux_applicationendorsements", service.RetrieveMultiple(new FetchExpression(fetch1)).Entities[0].Id);
                                //    }
                                //}
                            }

                            if (item.Attributes.Contains("lux_typeofheating"))
                            {
                                var Endorsement12 = service.RetrieveMultiple(new FetchExpression(endorsementFetch)).Entities.FirstOrDefault(x => x.Attributes["lux_name"].ToString() == "Wood Burner Condition");
                                if (Endorsement12 != null)
                                {
                                    var fetch1 = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                                                          <entity name='lux_applicationendorsements'>
                                                            <attribute name='lux_applicationendorsementsid' />
                                                            <attribute name='lux_name' />
                                                            <attribute name='lux_endorsementnumber' />
                                                            <attribute name='createdon' />
                                                            <order attribute='lux_name' descending='false' />
                                                            <filter type='and'>
                                                              <condition attribute='statecode' operator='eq' value='0' />
                                                              <condition attribute='lux_endorsementlibrary' operator='eq' uiname='' uitype='lux_endorsementlibrary' value='{Endorsement12.Attributes["lux_endorsementlibraryid"]}' />
                                                              <condition attribute='lux_application' operator='eq' uiname='' uitype='lux_propertyownersapplications' value='{appln.Id}' />
                                                            </filter>
                                                          </entity>
                                                        </fetch>";
                                    var HeatingType = item.GetAttributeValue<OptionSetValueCollection>("lux_typeofheating");
                                    if (HeatingType.Contains(new OptionSetValue(972970010)))
                                    {
                                        if (service.RetrieveMultiple(new FetchExpression(fetch1)).Entities.Count == 0)
                                        {
                                            Entity ent = new Entity("lux_applicationendorsements");
                                            ent["lux_isdefault"] = true;
                                            ent["lux_endorsementnumber"] = Endorsement12.Attributes["new_endorsementnumber"];
                                            ent["lux_endorsementhtml"] = Endorsement12.Attributes["lux_endorsementhtml"];
                                            ent["lux_name"] = Endorsement12.Attributes["lux_name"];
                                            ent["lux_endorsementlibrary"] = new EntityReference("lux_endorsementlibrary", new Guid(Endorsement12.Attributes["lux_endorsementlibraryid"].ToString()));
                                            ent["lux_application"] = new EntityReference("lux_propertyownersapplications", appln.Id);
                                            service.Create(ent);
                                        }
                                    }
                                    //else
                                    //{
                                    //    if (service.RetrieveMultiple(new FetchExpression(fetch1)).Entities.Count > 0)
                                    //    {
                                    //        service.Delete("lux_applicationendorsements", service.RetrieveMultiple(new FetchExpression(fetch1)).Entities[0].Id);
                                    //    }
                                    //}
                                }

                                var Endorsement17 = service.RetrieveMultiple(new FetchExpression(endorsementFetch)).Entities.FirstOrDefault(x => x.Attributes["lux_name"].ToString() == "Chimney Sweeping and Open fires");
                                if (Endorsement17 != null)
                                {
                                    var fetch1 = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                                                          <entity name='lux_applicationendorsements'>
                                                            <attribute name='lux_applicationendorsementsid' />
                                                            <attribute name='lux_name' />
                                                            <attribute name='lux_endorsementnumber' />
                                                            <attribute name='createdon' />
                                                            <order attribute='lux_name' descending='false' />
                                                            <filter type='and'>
                                                              <condition attribute='statecode' operator='eq' value='0' />
                                                              <condition attribute='lux_endorsementlibrary' operator='eq' uiname='' uitype='lux_endorsementlibrary' value='{Endorsement17.Attributes["lux_endorsementlibraryid"]}' />
                                                              <condition attribute='lux_application' operator='eq' uiname='' uitype='lux_propertyownersapplications' value='{appln.Id}' />
                                                            </filter>
                                                          </entity>
                                                        </fetch>";
                                    var HeatingType = item.GetAttributeValue<OptionSetValueCollection>("lux_typeofheating");
                                    if (HeatingType.Contains(new OptionSetValue(972970010)))
                                    {
                                        if (service.RetrieveMultiple(new FetchExpression(fetch1)).Entities.Count == 0)
                                        {
                                            Entity ent = new Entity("lux_applicationendorsements");
                                            ent["lux_isdefault"] = true;
                                            ent["lux_endorsementnumber"] = Endorsement17.Attributes["new_endorsementnumber"];
                                            ent["lux_endorsementhtml"] = Endorsement17.Attributes["lux_endorsementhtml"];
                                            ent["lux_name"] = Endorsement17.Attributes["lux_name"];
                                            ent["lux_endorsementlibrary"] = new EntityReference("lux_endorsementlibrary", new Guid(Endorsement17.Attributes["lux_endorsementlibraryid"].ToString()));
                                            ent["lux_application"] = new EntityReference("lux_propertyownersapplications", appln.Id);
                                            service.Create(ent);
                                        }
                                    }
                                    //else
                                    //{
                                    //    if (service.RetrieveMultiple(new FetchExpression(fetch1)).Entities.Count > 0)
                                    //    {
                                    //        service.Delete("lux_applicationendorsements", service.RetrieveMultiple(new FetchExpression(fetch1)).Entities[0].Id);
                                    //    }
                                    //}
                                }
                            }

                            var Endorsement13 = service.RetrieveMultiple(new FetchExpression(endorsementFetch)).Entities.FirstOrDefault(x => x.Attributes["lux_name"].ToString() == "Work Away Exclusion - Public and Products Liability");
                            if (Endorsement13 != null)
                            {
                                var fetch1 = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                                                          <entity name='lux_applicationendorsements'>
                                                            <attribute name='lux_applicationendorsementsid' />
                                                            <attribute name='lux_name' />
                                                            <attribute name='lux_endorsementnumber' />
                                                            <attribute name='createdon' />
                                                            <order attribute='lux_name' descending='false' />
                                                            <filter type='and'>
                                                              <condition attribute='statecode' operator='eq' value='0' />
                                                              <condition attribute='lux_endorsementlibrary' operator='eq' uiname='' uitype='lux_endorsementlibrary' value='{Endorsement13.Attributes["lux_endorsementlibraryid"]}' />
                                                              <condition attribute='lux_application' operator='eq' uiname='' uitype='lux_propertyownersapplications' value='{appln.Id}' />
                                                            </filter>
                                                          </entity>
                                                        </fetch>";

                                if (WorkAway == false)
                                {
                                    if (service.RetrieveMultiple(new FetchExpression(fetch1)).Entities.Count == 0)
                                    {
                                        Entity ent = new Entity("lux_applicationendorsements");
                                        ent["lux_isdefault"] = true;
                                        ent["lux_endorsementhtml"] = Endorsement13.Attributes.Contains("lux_endorsementhtml") ? Endorsement13.Attributes["lux_endorsementhtml"] : "";
                                        ent["lux_endorsementnumber"] = Endorsement13.Attributes["new_endorsementnumber"];
                                        ent["lux_name"] = Endorsement13.Attributes["lux_name"];
                                        ent["lux_endorsementlibrary"] = new EntityReference("lux_endorsementlibrary", new Guid(Endorsement13.Attributes["lux_endorsementlibraryid"].ToString()));
                                        ent["lux_application"] = new EntityReference("lux_propertyownersapplications", appln.Id);
                                        service.Create(ent);
                                    }
                                }
                                //else
                                //{
                                //    if (service.RetrieveMultiple(new FetchExpression(fetch1)).Entities.Count > 0)
                                //    {
                                //        service.Delete("lux_applicationendorsements", service.RetrieveMultiple(new FetchExpression(fetch1)).Entities[0].Id);
                                //    }
                                //}
                            }

                            var Endorsement14 = service.RetrieveMultiple(new FetchExpression(endorsementFetch)).Entities.FirstOrDefault(x => x.Attributes["lux_name"].ToString() == "Alarm Requirement: Audible Signalling");
                            if (Endorsement14 != null)
                            {
                                var fetch1 = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                                                          <entity name='lux_applicationendorsements'>
                                                            <attribute name='lux_applicationendorsementsid' />
                                                            <attribute name='lux_name' />
                                                            <attribute name='lux_endorsementnumber' />
                                                            <attribute name='createdon' />
                                                            <order attribute='lux_name' descending='false' />
                                                            <filter type='and'>
                                                              <condition attribute='statecode' operator='eq' value='0' />
                                                              <condition attribute='lux_endorsementlibrary' operator='eq' uiname='' uitype='lux_endorsementlibrary' value='{Endorsement14.Attributes["lux_endorsementlibraryid"]}' />
                                                              <condition attribute='lux_application' operator='eq' uiname='' uitype='lux_propertyownersapplications' value='{appln.Id}' />
                                                            </filter>
                                                          </entity>
                                                        </fetch>";

                                if (IntruderAlarm == true && TypeOFAlarm == 972970001)//audible only
                                {
                                    if (service.RetrieveMultiple(new FetchExpression(fetch1)).Entities.Count == 0)
                                    {
                                        Entity ent = new Entity("lux_applicationendorsements");
                                        ent["lux_isdefault"] = true;
                                        ent["lux_endorsementhtml"] = Endorsement14.Attributes.Contains("lux_endorsementhtml") ? Endorsement14.Attributes["lux_endorsementhtml"] : "";
                                        ent["lux_endorsementnumber"] = Endorsement14.Attributes["new_endorsementnumber"];
                                        ent["lux_name"] = Endorsement14.Attributes["lux_name"];
                                        ent["lux_endorsementlibrary"] = new EntityReference("lux_endorsementlibrary", new Guid(Endorsement14.Attributes["lux_endorsementlibraryid"].ToString()));
                                        ent["lux_application"] = new EntityReference("lux_propertyownersapplications", appln.Id);
                                        service.Create(ent);
                                    }
                                }
                                //else
                                //{
                                //    if (service.RetrieveMultiple(new FetchExpression(fetch1)).Entities.Count > 0)
                                //    {
                                //        service.Delete("lux_applicationendorsements", service.RetrieveMultiple(new FetchExpression(fetch1)).Entities[0].Id);
                                //    }
                                //}
                            }

                            var Endorsement15 = service.RetrieveMultiple(new FetchExpression(endorsementFetch)).Entities.FirstOrDefault(x => x.Attributes["lux_name"].ToString() == "Alarm Requirement: Non-Confirmed Signalling");
                            if (Endorsement15 != null)
                            {
                                var fetch1 = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                                                          <entity name='lux_applicationendorsements'>
                                                            <attribute name='lux_applicationendorsementsid' />
                                                            <attribute name='lux_name' />
                                                            <attribute name='lux_endorsementnumber' />
                                                            <attribute name='createdon' />
                                                            <order attribute='lux_name' descending='false' />
                                                            <filter type='and'>
                                                              <condition attribute='statecode' operator='eq' value='0' />
                                                              <condition attribute='lux_endorsementlibrary' operator='eq' uiname='' uitype='lux_endorsementlibrary' value='{Endorsement15.Attributes["lux_endorsementlibraryid"]}' />
                                                              <condition attribute='lux_application' operator='eq' uiname='' uitype='lux_propertyownersapplications' value='{appln.Id}' />
                                                            </filter>
                                                          </entity>
                                                        </fetch>";

                                if (IntruderAlarm == true && (TypeOFAlarm == 972970002 || TypeOFAlarm == 972970005))
                                {
                                    if (service.RetrieveMultiple(new FetchExpression(fetch1)).Entities.Count == 0)
                                    {
                                        Entity ent = new Entity("lux_applicationendorsements");
                                        ent["lux_isdefault"] = true;
                                        ent["lux_endorsementhtml"] = Endorsement15.Attributes.Contains("lux_endorsementhtml") ? Endorsement15.Attributes["lux_endorsementhtml"] : "";
                                        ent["lux_endorsementnumber"] = Endorsement15.Attributes["new_endorsementnumber"];
                                        ent["lux_name"] = Endorsement15.Attributes["lux_name"];
                                        ent["lux_endorsementlibrary"] = new EntityReference("lux_endorsementlibrary", new Guid(Endorsement15.Attributes["lux_endorsementlibraryid"].ToString()));
                                        ent["lux_application"] = new EntityReference("lux_propertyownersapplications", appln.Id);
                                        service.Create(ent);
                                    }
                                }
                                //else
                                //{
                                //    if (service.RetrieveMultiple(new FetchExpression(fetch1)).Entities.Count > 0)
                                //    {
                                //        service.Delete("lux_applicationendorsements", service.RetrieveMultiple(new FetchExpression(fetch1)).Entities[0].Id);
                                //    }
                                //}
                            }

                            var Endorsement16 = service.RetrieveMultiple(new FetchExpression(endorsementFetch)).Entities.FirstOrDefault(x => x.Attributes["lux_name"].ToString() == "Alarm Requirement: Confirmed Signalling");
                            if (Endorsement16 != null)
                            {
                                var fetch1 = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                                                          <entity name='lux_applicationendorsements'>
                                                            <attribute name='lux_applicationendorsementsid' />
                                                            <attribute name='lux_name' />
                                                            <attribute name='lux_endorsementnumber' />
                                                            <attribute name='createdon' />
                                                            <order attribute='lux_name' descending='false' />
                                                            <filter type='and'>
                                                              <condition attribute='statecode' operator='eq' value='0' />
                                                              <condition attribute='lux_endorsementlibrary' operator='eq' uiname='' uitype='lux_endorsementlibrary' value='{Endorsement16.Attributes["lux_endorsementlibraryid"]}' />
                                                              <condition attribute='lux_application' operator='eq' uiname='' uitype='lux_propertyownersapplications' value='{appln.Id}' />
                                                            </filter>
                                                          </entity>
                                                        </fetch>";

                                if (IntruderAlarm == true && (TypeOFAlarm == 972970003 || TypeOFAlarm == 972970004))//redcare gsm or dualcom
                                {
                                    if (service.RetrieveMultiple(new FetchExpression(fetch1)).Entities.Count == 0)
                                    {
                                        Entity ent = new Entity("lux_applicationendorsements");
                                        ent["lux_isdefault"] = true;
                                        ent["lux_endorsementhtml"] = Endorsement16.Attributes.Contains("lux_endorsementhtml") ? Endorsement16.Attributes["lux_endorsementhtml"] : "";
                                        ent["lux_endorsementnumber"] = Endorsement16.Attributes["new_endorsementnumber"];
                                        ent["lux_name"] = Endorsement16.Attributes["lux_name"];
                                        ent["lux_endorsementlibrary"] = new EntityReference("lux_endorsementlibrary", new Guid(Endorsement16.Attributes["lux_endorsementlibraryid"].ToString()));
                                        ent["lux_application"] = new EntityReference("lux_propertyownersapplications", appln.Id);
                                        service.Create(ent);
                                    }
                                }
                                //else
                                //{
                                //    if (service.RetrieveMultiple(new FetchExpression(fetch1)).Entities.Count > 0)
                                //    {
                                //        service.Delete("lux_applicationendorsements", service.RetrieveMultiple(new FetchExpression(fetch1)).Entities[0].Id);
                                //    }
                                //}
                            }

                            var Endorsement19 = service.RetrieveMultiple(new FetchExpression(endorsementFetch)).Entities.FirstOrDefault(x => x.Attributes["lux_name"].ToString() == "Work Away Exclusion - Employers Liability");
                            if (Endorsement19 != null)
                            {
                                var fetch1 = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                                                          <entity name='lux_applicationendorsements'>
                                                            <attribute name='lux_applicationendorsementsid' />
                                                            <attribute name='lux_name' />
                                                            <attribute name='lux_endorsementnumber' />
                                                            <attribute name='createdon' />
                                                            <order attribute='lux_name' descending='false' />
                                                            <filter type='and'>
                                                              <condition attribute='statecode' operator='eq' value='0' />
                                                              <condition attribute='lux_endorsementlibrary' operator='eq' uiname='' uitype='lux_endorsementlibrary' value='{Endorsement19.Attributes["lux_endorsementlibraryid"]}' />
                                                              <condition attribute='lux_application' operator='eq' uiname='' uitype='lux_propertyownersapplications' value='{appln.Id}' />
                                                            </filter>
                                                          </entity>
                                                        </fetch>";

                                if (WorkAway == false)
                                {
                                    if (service.RetrieveMultiple(new FetchExpression(fetch1)).Entities.Count == 0)
                                    {
                                        Entity ent = new Entity("lux_applicationendorsements");
                                        ent["lux_isdefault"] = true;
                                        ent["lux_endorsementhtml"] = Endorsement19.Attributes.Contains("lux_endorsementhtml") ? Endorsement19.Attributes["lux_endorsementhtml"] : "";
                                        ent["lux_endorsementnumber"] = Endorsement19.Attributes["new_endorsementnumber"];
                                        ent["lux_name"] = Endorsement19.Attributes["lux_name"];
                                        ent["lux_endorsementlibrary"] = new EntityReference("lux_endorsementlibrary", new Guid(Endorsement19.Attributes["lux_endorsementlibraryid"].ToString()));
                                        ent["lux_application"] = new EntityReference("lux_propertyownersapplications", appln.Id);
                                        service.Create(ent);
                                    }
                                }
                                //else
                                //{
                                //    if (service.RetrieveMultiple(new FetchExpression(fetch1)).Entities.Count > 0)
                                //    {
                                //        service.Delete("lux_applicationendorsements", service.RetrieveMultiple(new FetchExpression(fetch1)).Entities[0].Id);
                                //    }
                                //}
                            }

                            var Endorsement20 = service.RetrieveMultiple(new FetchExpression(endorsementFetch)).Entities.FirstOrDefault(x => x.Attributes["lux_name"].ToString() == "Products Exclusion");
                            if (Endorsement20 != null)
                            {
                                var fetch1 = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                                                          <entity name='lux_applicationendorsements'>
                                                            <attribute name='lux_applicationendorsementsid' />
                                                            <attribute name='lux_name' />
                                                            <attribute name='lux_endorsementnumber' />
                                                            <attribute name='createdon' />
                                                            <order attribute='lux_name' descending='false' />
                                                            <filter type='and'>
                                                              <condition attribute='statecode' operator='eq' value='0' />
                                                              <condition attribute='lux_endorsementlibrary' operator='eq' uiname='' uitype='lux_endorsementlibrary' value='{Endorsement20.Attributes["lux_endorsementlibraryid"]}' />
                                                              <condition attribute='lux_application' operator='eq' uiname='' uitype='lux_propertyownersapplications' value='{appln.Id}' />
                                                            </filter>
                                                          </entity>
                                                        </fetch>";

                                if (ProdusHazardGrade >= 6)
                                {
                                    if (service.RetrieveMultiple(new FetchExpression(fetch1)).Entities.Count == 0)
                                    {
                                        Entity ent = new Entity("lux_applicationendorsements");
                                        ent["lux_isdefault"] = true;
                                        ent["lux_endorsementnumber"] = Endorsement20.Attributes["new_endorsementnumber"];
                                        ent["lux_endorsementhtml"] = Endorsement20.Attributes["lux_endorsementhtml"];
                                        ent["lux_name"] = Endorsement20.Attributes["lux_name"];
                                        ent["lux_endorsementlibrary"] = new EntityReference("lux_endorsementlibrary", new Guid(Endorsement20.Attributes["lux_endorsementlibraryid"].ToString()));
                                        ent["lux_application"] = new EntityReference("lux_propertyownersapplications", appln.Id);
                                        service.Create(ent);
                                    }
                                }
                                //else
                                //{
                                //    if (service.RetrieveMultiple(new FetchExpression(fetch1)).Entities.Count > 0)
                                //    {
                                //        service.Delete("lux_applicationendorsements", service.RetrieveMultiple(new FetchExpression(fetch1)).Entities[0].Id);
                                //    }
                                //}
                            }

                            //if (model.IsLive == 0)
                            //{
                            var Endorsement9 = service.RetrieveMultiple(new FetchExpression(endorsementFetch)).Entities.FirstOrDefault(x => x.Attributes["lux_name"].ToString() == "Flat Roof - Increased Excess");
                            if (Endorsement9 != null)
                            {
                                var percent = item.Attributes.Contains("lux_flatroofpercentage") ? item.Attributes["lux_flatroofpercentage"].ToString().Replace("%", "") : "0";
                                percent = Regex.Match(percent, @"\d+").Value;

                                var fetch1 = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                                                              <entity name='lux_applicationendorsements'>
                                                                <attribute name='lux_applicationendorsementsid' />
                                                                <attribute name='lux_name' />
                                                                <attribute name='lux_endorsementnumber' />
                                                                <attribute name='createdon' />
                                                                <order attribute='lux_name' descending='false' />
                                                                <filter type='and'>
                                                                  <condition attribute='statecode' operator='eq' value='0' />
                                                                  <condition attribute='lux_endorsementlibrary' operator='eq' uiname='Wood Burner Condition' uitype='lux_endorsementlibrary' value='{Endorsement9.Attributes["lux_endorsementlibraryid"]}' />
                                                                  <condition attribute='lux_application' operator='eq' uiname='' uitype='lux_propertyownersapplications' value='{appln.Id}' />
                                                                </filter>
                                                              </entity>
                                                            </fetch>";

                                if (FlatRoof == true && Convert.ToDecimal(percent) > 50) // Flat roof
                                {
                                    if (service.RetrieveMultiple(new FetchExpression(fetch1)).Entities.Count == 0)
                                    {
                                        Entity ent = new Entity("lux_applicationendorsements");
                                        ent["lux_isdefault"] = true;
                                        ent["lux_endorsementhtml"] = Endorsement9.Attributes["lux_endorsementhtml"];
                                        ent["lux_endorsementnumber"] = Endorsement9.Attributes["new_endorsementnumber"];
                                        ent["lux_name"] = Endorsement9.Attributes["lux_name"];
                                        ent["lux_endorsementlibrary"] = new EntityReference("lux_endorsementlibrary", new Guid(Endorsement9.Attributes["lux_endorsementlibraryid"].ToString()));
                                        ent["lux_application"] = new EntityReference("lux_propertyownersapplications", appln.Id);
                                        service.Create(ent);
                                    }
                                }
                                //else
                                //{
                                //    if (service.RetrieveMultiple(new FetchExpression(fetch1)).Entities.Count > 0)
                                //    {
                                //        service.Delete("lux_applicationendorsements", service.RetrieveMultiple(new FetchExpression(fetch1)).Entities[0].Id);
                                //    }
                                //}
                            }

                            if (productType == 972970002)
                            {
                                var Endorsement21 = service.RetrieveMultiple(new FetchExpression(endorsementFetch)).Entities.FirstOrDefault(x => x.Attributes["lux_name"].ToString() == "Electrical Inspection and Testing Programme");
                                if (Endorsement21 != null)
                                {
                                    var fetch1 = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                                                              <entity name='lux_applicationendorsements'>
                                                                <attribute name='lux_applicationendorsementsid' />
                                                                <attribute name='lux_name' />
                                                                <attribute name='lux_endorsementnumber' />
                                                                <attribute name='createdon' />
                                                                <order attribute='lux_name' descending='false' />
                                                                <filter type='and'>
                                                                  <condition attribute='statecode' operator='eq' value='0' />
                                                                  <condition attribute='lux_endorsementlibrary' operator='eq' uiname='' uitype='lux_endorsementlibrary' value='{Endorsement21.Attributes["lux_endorsementlibraryid"]}' />
                                                                  <condition attribute='lux_application' operator='eq' uiname='' uitype='lux_propertyownersapplications' value='{appln.Id}' />
                                                                </filter>
                                                              </entity>
                                                            </fetch>";

                                    if (service.RetrieveMultiple(new FetchExpression(fetch1)).Entities.Count == 0)
                                    {
                                        Entity ent = new Entity("lux_applicationendorsements");
                                        ent["lux_application"] = new EntityReference("lux_propertyownersapplications", appln.Id);
                                        ent["lux_isdefault"] = true;
                                        ent["lux_endorsementnumber"] = Endorsement21.Attributes["new_endorsementnumber"];
                                        ent["lux_endorsementhtml"] = Endorsement21.Attributes["lux_endorsementhtml"];
                                        ent["lux_name"] = Endorsement21.Attributes["lux_name"];
                                        ent["lux_endorsementlibrary"] = new EntityReference("lux_endorsementlibrary", new Guid(Endorsement21.Attributes["lux_endorsementlibraryid"].ToString()));
                                        service.Create(ent);
                                    }
                                }

                                var Endorsement22 = service.RetrieveMultiple(new FetchExpression(endorsementFetch)).Entities.FirstOrDefault(x => x.Attributes["lux_name"].ToString() == "No Smoking Condition");
                                if (Endorsement22 != null)
                                {
                                    var fetch1 = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                                                              <entity name='lux_applicationendorsements'>
                                                                <attribute name='lux_applicationendorsementsid' />
                                                                <attribute name='lux_name' />
                                                                <attribute name='lux_endorsementnumber' />
                                                                <attribute name='createdon' />
                                                                <order attribute='lux_name' descending='false' />
                                                                <filter type='and'>
                                                                  <condition attribute='statecode' operator='eq' value='0' />
                                                                  <condition attribute='lux_endorsementlibrary' operator='eq' uiname='' uitype='lux_endorsementlibrary' value='{Endorsement22.Attributes["lux_endorsementlibraryid"]}' />
                                                                  <condition attribute='lux_application' operator='eq' uiname='' uitype='lux_propertyownersapplications' value='{appln.Id}' />
                                                                </filter>
                                                              </entity>
                                                            </fetch>";

                                    if (service.RetrieveMultiple(new FetchExpression(fetch1)).Entities.Count == 0)
                                    {
                                        Entity ent = new Entity("lux_applicationendorsements");
                                        ent["lux_application"] = new EntityReference("lux_propertyownersapplications", appln.Id);
                                        ent["lux_isdefault"] = true;
                                        ent["lux_endorsementnumber"] = Endorsement22.Attributes["new_endorsementnumber"];
                                        ent["lux_endorsementhtml"] = Endorsement22.Attributes["lux_endorsementhtml"];
                                        ent["lux_name"] = Endorsement22.Attributes["lux_name"];
                                        ent["lux_endorsementlibrary"] = new EntityReference("lux_endorsementlibrary", new Guid(Endorsement22.Attributes["lux_endorsementlibraryid"].ToString()));
                                        service.Create(ent);
                                    }
                                }
                            }
                            //}
                        }
                    }
                }
            }
            else if (productName == "Hotels and Guesthouses")
            {
                var fetch = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                                      <entity name='lux_pubsrestaurantspropertyownersapplicatio'>
                                        <attribute name='lux_pubsrestaurantspropertyownersapplicatioid' />
                                        <attribute name='lux_name' />
                                        <attribute name='createdon' />
                                        <attribute name='lux_rentreceivable' />
                                        <attribute name='lux_icow' />
                                        <attribute name='lux_typeofcover' />
                                        <attribute name='lux_bookdebts' />
                                        <attribute name='lux_issubsidencecoverrequired' />
                                        <attribute name='lux_amount' />
                                        <attribute name='lux_maintradeforthispremises' />
                                        <attribute name='lux_additionalincreasedcostofworking' />
                                        <attribute name='lux_doesthepremiseshaveaflatroof' />
                                        <attribute name='lux_flatroofpercentage' />
                                        <attribute name='lux_doesthepremiseshaveabasement' />
                                        <attribute name='lux_isthereanatm' />     
                                        <attribute name='lux_isthereanintruderalarminstalledandinworki' />                                
                                        <attribute name='lux_whattypeofalarmisinstalled' />
                                        <attribute name='lux_typeofheating' />
                                        <attribute name='lux_isownerresponsibleforfillingandemptyingth' />
                                        <attribute name='lux_isthereanydeepfatfrying' />
                                        <attribute name='lux_floodscore' />
                                        <attribute name='lux_subsidencescore' />
                                        <attribute name='lux_crimescore' />
                                        <attribute name='lux_securityrating' />
                                        <order attribute='lux_name' descending='false' />
                                        <filter type='and'>
                                          <condition attribute='lux_propertyownersapplications' operator='eq' uiname='' uitype='lux_propertyownersapplications' value='{appln.Id}' />
                                        </filter>
                                        <link-entity name='lux_propertyownersapplications' from='lux_propertyownersapplicationsid' to='lux_propertyownersapplications' visible='false' link-type='outer' alias='appln'>
                                          <attribute name='lux_anyheatworkawayundertaken' />
                                          <attribute name='lux_isanyworkawaycarriedoutotherthanforcollec' />
                                        </link-entity>
                                      </entity>
                                    </fetch>";

                if (service.RetrieveMultiple(new FetchExpression(fetch)).Entities.Count > 0)
                {
                    var endorsementFetch = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                                                          <entity name='lux_endorsementlibrary'>
                                                            <attribute name='lux_name' />
                                                            <attribute name='new_product' />
                                                            <attribute name='lux_insurer' />
                                                            <attribute name='lux_endorsementdescription' />
                                                            <attribute name='lux_endorsementhtml' />
                                                            <attribute name='new_endorsementnumber' />
                                                            <attribute name='lux_endorsementlibraryid' />
                                                            <order attribute='lux_name' descending='false' />
                                                            <filter type='and'>
                                                              <condition attribute='statecode' operator='eq' value='0' />
                                                              <condition attribute='lux_name' operator='not-null' />
                                                              <filter type='or'>
                                                                <condition attribute='new_product' operator='eq' uiname='' uitype='product' value='{"a4d771a3-aaa1-eb11-b1ac-00224840d300"}' />
                                                                <condition attribute='new_product' operator='null' />
                                                              </filter>
                                                            </filter>
                                                          </entity>
                                                        </fetch>";

                    foreach (var item in service.RetrieveMultiple(new FetchExpression(fetch)).Entities)
                    {
                        var FlatRoof = item.Attributes.Contains("lux_doesthepremiseshaveaflatroof") ? item.GetAttributeValue<bool>("lux_doesthepremiseshaveaflatroof") : false;
                        var ATM = item.Attributes.Contains("lux_isthereanatm") ? item.GetAttributeValue<bool>("lux_isthereanatm") : false;
                        var OwnerResponsible = item.Attributes.Contains("lux_isownerresponsibleforfillingandemptyingth") ? item.GetAttributeValue<bool>("lux_isownerresponsibleforfillingandemptyingth") : false;
                        var AnyDFF = item.Attributes.Contains("lux_isthereanydeepfatfrying") ? item.GetAttributeValue<bool>("lux_isthereanydeepfatfrying") : false;
                        var AnyHeatWorkAway = item.Attributes.Contains("appln.lux_anyheatworkawayundertaken") ? ((bool)(item.GetAttributeValue<AliasedValue>("appln.lux_anyheatworkawayundertaken").Value)) : false;
                        var WorkAway = item.Attributes.Contains("appln.lux_isanyworkawaycarriedoutotherthanforcollec") ? ((bool)(item.GetAttributeValue<AliasedValue>("appln.lux_isanyworkawaycarriedoutotherthanforcollec").Value)) : false;
                        var IntruderAlarm = item.Attributes.Contains("lux_isthereanintruderalarminstalledandinworki") ? item.GetAttributeValue<bool>("lux_isthereanintruderalarminstalledandinworki") : false;
                        var TypeOFAlarm = item.Attributes.Contains("lux_whattypeofalarmisinstalled") ? item.GetAttributeValue<OptionSetValue>("lux_whattypeofalarmisinstalled").Value : 0;
                        var Basement = item.Attributes.Contains("lux_doesthepremiseshaveabasement") ? item.GetAttributeValue<bool>("lux_doesthepremiseshaveabasement") : false;

                        if (service.RetrieveMultiple(new FetchExpression(endorsementFetch)).Entities.Count > 0)
                        {
                            var Endorsement1 = new Entity();
                            if (OwnerResponsible == true)
                            {
                                Endorsement1 = service.RetrieveMultiple(new FetchExpression(endorsementFetch)).Entities.FirstOrDefault(x => x.Attributes["lux_name"].ToString() == "Automated Teller Machine (ATM) - Self Fill");
                            }
                            else
                            {
                                Endorsement1 = service.RetrieveMultiple(new FetchExpression(endorsementFetch)).Entities.FirstOrDefault(x => x.Attributes["lux_name"].ToString() == "Automated Teller Machine (ATM) - Third Party Provided & Maintained Machines");
                            }

                            if (Endorsement1 != null)
                            {
                                var fetch1 = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                                                          <entity name='lux_applicationendorsements'>
                                                            <attribute name='lux_applicationendorsementsid' />
                                                            <attribute name='lux_name' />
                                                            <attribute name='lux_endorsementnumber' />
                                                            <attribute name='createdon' />
                                                            <order attribute='lux_name' descending='false' />
                                                            <filter type='and'>
                                                              <condition attribute='statecode' operator='eq' value='0' />
                                                              <condition attribute='lux_endorsementlibrary' operator='eq' uiname='' uitype='lux_endorsementlibrary' value='{Endorsement1.Attributes["lux_endorsementlibraryid"]}' />
                                                              <condition attribute='lux_application' operator='eq' uiname='' uitype='lux_propertyownersapplications' value='{appln.Id}' />
                                                            </filter>
                                                          </entity>
                                                        </fetch>";

                                if (ATM == true) // Flat roof
                                {
                                    if (service.RetrieveMultiple(new FetchExpression(fetch1)).Entities.Count == 0)
                                    {
                                        Entity ent = new Entity("lux_applicationendorsements");
                                        ent["lux_isdefault"] = true;
                                        ent["lux_endorsementhtml"] = Endorsement1.Attributes.Contains("lux_endorsementhtml") ? Endorsement1.Attributes["lux_endorsementhtml"] : "";
                                        ent["lux_endorsementnumber"] = Endorsement1.Attributes["new_endorsementnumber"];
                                        ent["lux_name"] = Endorsement1.Attributes["lux_name"];
                                        ent["lux_endorsementlibrary"] = new EntityReference("lux_endorsementlibrary", new Guid(Endorsement1.Attributes["lux_endorsementlibraryid"].ToString()));
                                        ent["lux_application"] = new EntityReference("lux_propertyownersapplications", appln.Id);
                                        service.Create(ent);
                                    }
                                }
                                //else
                                //{
                                //    if (service.RetrieveMultiple(new FetchExpression(fetch1)).Entities.Count > 0)
                                //    {
                                //        service.Delete("lux_applicationendorsements", service.RetrieveMultiple(new FetchExpression(fetch1)).Entities[0].Id);
                                //    }
                                //}
                            }

                            var Endorsement2 = service.RetrieveMultiple(new FetchExpression(endorsementFetch)).Entities.FirstOrDefault(x => x.Attributes["lux_name"].ToString() == "Kitchen Precautions");
                            if (Endorsement2 != null)
                            {
                                var fetch1 = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                                                          <entity name='lux_applicationendorsements'>
                                                            <attribute name='lux_applicationendorsementsid' />
                                                            <attribute name='lux_name' />
                                                            <attribute name='lux_endorsementnumber' />
                                                            <attribute name='createdon' />
                                                            <order attribute='lux_name' descending='false' />
                                                            <filter type='and'>
                                                              <condition attribute='statecode' operator='eq' value='0' />
                                                              <condition attribute='lux_endorsementlibrary' operator='eq' uiname='' uitype='lux_endorsementlibrary' value='{Endorsement2.Attributes["lux_endorsementlibraryid"]}' />
                                                              <condition attribute='lux_application' operator='eq' uiname='' uitype='lux_propertyownersapplications' value='{appln.Id}' />
                                                            </filter>
                                                          </entity>
                                                        </fetch>";

                                if (AnyDFF == true) // Flat roof
                                {
                                    if (service.RetrieveMultiple(new FetchExpression(fetch1)).Entities.Count == 0)
                                    {
                                        Entity ent = new Entity("lux_applicationendorsements");
                                        ent["lux_isdefault"] = true;
                                        ent["lux_endorsementhtml"] = Endorsement2.Attributes.Contains("lux_endorsementhtml") ? Endorsement2.Attributes["lux_endorsementhtml"] : "";
                                        ent["lux_endorsementnumber"] = Endorsement2.Attributes["new_endorsementnumber"];
                                        ent["lux_name"] = Endorsement2.Attributes["lux_name"];
                                        ent["lux_endorsementlibrary"] = new EntityReference("lux_endorsementlibrary", new Guid(Endorsement2.Attributes["lux_endorsementlibraryid"].ToString()));
                                        ent["lux_application"] = new EntityReference("lux_propertyownersapplications", appln.Id);
                                        service.Create(ent);
                                    }
                                }
                                //else
                                //{
                                //    if (service.RetrieveMultiple(new FetchExpression(fetch1)).Entities.Count > 0)
                                //    {
                                //        service.Delete("lux_applicationendorsements", service.RetrieveMultiple(new FetchExpression(fetch1)).Entities[0].Id);
                                //    }
                                //}
                            }

                            var Endorsement3 = service.RetrieveMultiple(new FetchExpression(endorsementFetch)).Entities.FirstOrDefault(x => x.Attributes["lux_name"].ToString() == "Roof Maintenance Condition");
                            if (Endorsement3 != null)
                            {
                                var fetch1 = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                                                          <entity name='lux_applicationendorsements'>
                                                            <attribute name='lux_applicationendorsementsid' />
                                                            <attribute name='lux_name' />
                                                            <attribute name='lux_endorsementnumber' />
                                                            <attribute name='createdon' />
                                                            <order attribute='lux_name' descending='false' />
                                                            <filter type='and'>
                                                              <condition attribute='statecode' operator='eq' value='0' />
                                                              <condition attribute='lux_endorsementlibrary' operator='eq' uiname='' uitype='lux_endorsementlibrary' value='{Endorsement3.Attributes["lux_endorsementlibraryid"]}' />
                                                              <condition attribute='lux_application' operator='eq' uiname='' uitype='lux_propertyownersapplications' value='{appln.Id}' />
                                                            </filter>
                                                          </entity>
                                                        </fetch>";

                                if (FlatRoof == true) // Flat roof
                                {
                                    if (service.RetrieveMultiple(new FetchExpression(fetch1)).Entities.Count == 0)
                                    {
                                        Entity ent = new Entity("lux_applicationendorsements");
                                        ent["lux_isdefault"] = true;
                                        ent["lux_endorsementhtml"] = Endorsement3.Attributes.Contains("lux_endorsementhtml") ? Endorsement3.Attributes["lux_endorsementhtml"] : "";
                                        ent["lux_endorsementnumber"] = Endorsement3.Attributes["new_endorsementnumber"];
                                        ent["lux_name"] = Endorsement3.Attributes["lux_name"];
                                        ent["lux_endorsementlibrary"] = new EntityReference("lux_endorsementlibrary", new Guid(Endorsement3.Attributes["lux_endorsementlibraryid"].ToString()));
                                        ent["lux_application"] = new EntityReference("lux_propertyownersapplications", appln.Id);
                                        service.Create(ent);
                                    }
                                }
                                //else
                                //{
                                //    if (service.RetrieveMultiple(new FetchExpression(fetch1)).Entities.Count > 0)
                                //    {
                                //        service.Delete("lux_applicationendorsements", service.RetrieveMultiple(new FetchExpression(fetch1)).Entities[0].Id);
                                //    }
                                //}
                            }

                            var Endorsement4 = service.RetrieveMultiple(new FetchExpression(endorsementFetch)).Entities.FirstOrDefault(x => x.Attributes["lux_name"].ToString() == "Heat Application Condition");
                            if (Endorsement4 != null)
                            {
                                var fetch1 = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                                                          <entity name='lux_applicationendorsements'>
                                                            <attribute name='lux_applicationendorsementsid' />
                                                            <attribute name='lux_name' />
                                                            <attribute name='lux_endorsementnumber' />
                                                            <attribute name='createdon' />
                                                            <order attribute='lux_name' descending='false' />
                                                            <filter type='and'>
                                                              <condition attribute='statecode' operator='eq' value='0' />
                                                              <condition attribute='lux_endorsementlibrary' operator='eq' uiname='' uitype='lux_endorsementlibrary' value='{Endorsement4.Attributes["lux_endorsementlibraryid"]}' />
                                                              <condition attribute='lux_application' operator='eq' uiname='' uitype='lux_propertyownersapplications' value='{appln.Id}' />
                                                            </filter>
                                                          </entity>
                                                        </fetch>";

                                if (AnyHeatWorkAway == true)
                                {
                                    if (service.RetrieveMultiple(new FetchExpression(fetch1)).Entities.Count == 0)
                                    {
                                        Entity ent = new Entity("lux_applicationendorsements");
                                        ent["lux_isdefault"] = true;
                                        ent["lux_endorsementhtml"] = Endorsement4.Attributes.Contains("lux_endorsementhtml") ? Endorsement4.Attributes["lux_endorsementhtml"] : "";
                                        ent["lux_endorsementnumber"] = Endorsement4.Attributes["new_endorsementnumber"];
                                        ent["lux_name"] = Endorsement4.Attributes["lux_name"];
                                        ent["lux_endorsementlibrary"] = new EntityReference("lux_endorsementlibrary", new Guid(Endorsement4.Attributes["lux_endorsementlibraryid"].ToString()));
                                        ent["lux_application"] = new EntityReference("lux_propertyownersapplications", appln.Id);
                                        service.Create(ent);
                                    }
                                }
                                //else
                                //{
                                //    if (service.RetrieveMultiple(new FetchExpression(fetch1)).Entities.Count > 0)
                                //    {
                                //        service.Delete("lux_applicationendorsements", service.RetrieveMultiple(new FetchExpression(fetch1)).Entities[0].Id);
                                //    }
                                //}
                            }

                            var FloodScore = item.Attributes.Contains("lux_floodscore") ? item.GetAttributeValue<int>("lux_floodscore") : 0;
                            var SubsidienceScore = item.Attributes.Contains("lux_subsidencescore") ? item.GetAttributeValue<int>("lux_subsidencescore") : 0;

                            var Endorsement5 = service.RetrieveMultiple(new FetchExpression(endorsementFetch)).Entities.FirstOrDefault(x => x.Attributes["lux_name"].ToString() == "Increased Flood Excess - £1,000");
                            if (Endorsement5 != null)
                            {
                                var fetch1 = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                                                          <entity name='lux_applicationendorsements'>
                                                            <attribute name='lux_applicationendorsementsid' />
                                                            <attribute name='lux_name' />
                                                            <attribute name='lux_endorsementnumber' />
                                                            <attribute name='createdon' />
                                                            <order attribute='lux_name' descending='false' />
                                                            <filter type='and'>
                                                              <condition attribute='statecode' operator='eq' value='0' />
                                                              <condition attribute='lux_endorsementlibrary' operator='eq' uiname='' uitype='lux_endorsementlibrary' value='{Endorsement5.Attributes["lux_endorsementlibraryid"]}' />
                                                              <condition attribute='lux_application' operator='eq' uiname='' uitype='lux_propertyownersapplications' value='{appln.Id}' />
                                                            </filter>
                                                          </entity>
                                                        </fetch>";

                                //if (model.IsLive == 0)
                                //{
                                if (FloodScore == 10 || Basement == true) //£1000 Flood Excess
                                {
                                    if (service.RetrieveMultiple(new FetchExpression(fetch1)).Entities.Count == 0)
                                    {
                                        Entity ent = new Entity("lux_applicationendorsements");
                                        ent["lux_isdefault"] = true;
                                        ent["lux_endorsementnumber"] = Endorsement5.Attributes["new_endorsementnumber"];
                                        ent["lux_endorsementhtml"] = Endorsement5.Attributes["lux_endorsementhtml"];
                                        ent["lux_name"] = Endorsement5.Attributes["lux_name"];
                                        ent["lux_endorsementlibrary"] = new EntityReference("lux_endorsementlibrary", new Guid(Endorsement5.Attributes["lux_endorsementlibraryid"].ToString()));
                                        ent["lux_application"] = new EntityReference("lux_propertyownersapplications", appln.Id);
                                        service.Create(ent);
                                    }
                                }
                                //else
                                //{
                                //    if (service.RetrieveMultiple(new FetchExpression(fetch1)).Entities.Count > 0)
                                //    {
                                //        service.Delete("lux_applicationendorsements", service.RetrieveMultiple(new FetchExpression(fetch1)).Entities[0].Id);
                                //    }
                                //}
                                //}
                                //else
                                //{
                                //    if (FloodScore == 10) //£1000 Flood Excess
                                //    {
                                //        if (service.RetrieveMultiple(new FetchExpression(fetch1)).Entities.Count == 0)
                                //        {
                                //            Entity ent = new Entity("lux_applicationendorsements");
                                //            ent["lux_isdefault"] = true;
                                //            ent["lux_endorsementnumber"] = Endorsement5.Attributes["new_endorsementnumber"];
                                //            ent["lux_endorsementhtml"] = Endorsement5.Attributes["lux_endorsementhtml"];
                                //            ent["lux_name"] = Endorsement5.Attributes["lux_name"];
                                //            ent["lux_endorsementlibrary"] = new EntityReference("lux_endorsementlibrary", new Guid(Endorsement5.Attributes["lux_endorsementlibraryid"].ToString()));
                                //            ent["lux_application"] = new EntityReference("lux_propertyownersapplications", appln.Id);
                                //            service.Create(ent);
                                //        }
                                //    }
                                //    //else
                                //    //{
                                //    //    if (service.RetrieveMultiple(new FetchExpression(fetch1)).Entities.Count > 0)
                                //    //    {
                                //    //        service.Delete("lux_applicationendorsements", service.RetrieveMultiple(new FetchExpression(fetch1)).Entities[0].Id);
                                //    //    }
                                //    //}
                                //}
                            }

                            var Endorsement6 = service.RetrieveMultiple(new FetchExpression(endorsementFetch)).Entities.FirstOrDefault(x => x.Attributes["lux_name"].ToString() == "Flood Exclusion");
                            if (Endorsement6 != null)
                            {
                                var fetch1 = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                                                          <entity name='lux_applicationendorsements'>
                                                            <attribute name='lux_applicationendorsementsid' />
                                                            <attribute name='lux_name' />
                                                            <attribute name='lux_endorsementnumber' />
                                                            <attribute name='createdon' />
                                                            <order attribute='lux_name' descending='false' />
                                                            <filter type='and'>
                                                              <condition attribute='statecode' operator='eq' value='0' />
                                                              <condition attribute='lux_endorsementlibrary' operator='eq' uiname='' uitype='lux_endorsementlibrary' value='{Endorsement6.Attributes["lux_endorsementlibraryid"]}' />
                                                              <condition attribute='lux_application' operator='eq' uiname='' uitype='lux_propertyownersapplications' value='{appln.Id}' />
                                                            </filter>
                                                          </entity>
                                                        </fetch>";
                                if (FloodScore >= 11) //Decline - Exclude Flood Cover
                                {
                                    if (service.RetrieveMultiple(new FetchExpression(fetch1)).Entities.Count == 0)
                                    {
                                        Entity ent = new Entity("lux_applicationendorsements");
                                        ent["lux_isdefault"] = true;
                                        ent["lux_endorsementnumber"] = Endorsement6.Attributes["new_endorsementnumber"];
                                        ent["lux_endorsementhtml"] = Endorsement6.Attributes["lux_endorsementhtml"];
                                        ent["lux_name"] = Endorsement6.Attributes["lux_name"];
                                        ent["lux_endorsementlibrary"] = new EntityReference("lux_endorsementlibrary", new Guid(Endorsement6.Attributes["lux_endorsementlibraryid"].ToString()));
                                        ent["lux_application"] = new EntityReference("lux_propertyownersapplications", appln.Id);
                                        service.Create(ent);
                                    }
                                }
                                //else
                                //{
                                //    if (service.RetrieveMultiple(new FetchExpression(fetch1)).Entities.Count > 0)
                                //    {
                                //        service.Delete("lux_applicationendorsements", service.RetrieveMultiple(new FetchExpression(fetch1)).Entities[0].Id);
                                //    }
                                //}
                            }

                            var Endorsement7 = service.RetrieveMultiple(new FetchExpression(endorsementFetch)).Entities.FirstOrDefault(x => x.Attributes["lux_name"].ToString() == "Increased Subsidence Excess - £2,500");
                            if (Endorsement7 != null)
                            {
                                var fetch1 = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                                                          <entity name='lux_applicationendorsements'>
                                                            <attribute name='lux_applicationendorsementsid' />
                                                            <attribute name='lux_name' />
                                                            <attribute name='lux_endorsementnumber' />
                                                            <attribute name='createdon' />
                                                            <order attribute='lux_name' descending='false' />
                                                            <filter type='and'>
                                                              <condition attribute='statecode' operator='eq' value='0' />
                                                              <condition attribute='lux_endorsementlibrary' operator='eq' uiname='' uitype='lux_endorsementlibrary' value='{Endorsement7.Attributes["lux_endorsementlibraryid"]}' />
                                                              <condition attribute='lux_application' operator='eq' uiname='' uitype='lux_propertyownersapplications' value='{appln.Id}' />
                                                            </filter>
                                                          </entity>
                                                        </fetch>";
                                if ((SubsidienceScore == 4 || SubsidienceScore == 5) && item.GetAttributeValue<bool>("lux_issubsidencecoverrequired") == true) //£750 Flood Excess
                                {
                                    if (service.RetrieveMultiple(new FetchExpression(fetch1)).Entities.Count == 0)
                                    {
                                        Entity ent = new Entity("lux_applicationendorsements");
                                        ent["lux_isdefault"] = true;
                                        ent["lux_endorsementnumber"] = Endorsement7.Attributes["new_endorsementnumber"];
                                        ent["lux_endorsementhtml"] = Endorsement7.Attributes["lux_endorsementhtml"];
                                        ent["lux_name"] = Endorsement7.Attributes["lux_name"];
                                        ent["lux_endorsementlibrary"] = new EntityReference("lux_endorsementlibrary", new Guid(Endorsement7.Attributes["lux_endorsementlibraryid"].ToString()));
                                        ent["lux_application"] = new EntityReference("lux_propertyownersapplications", appln.Id);
                                        service.Create(ent);
                                    }
                                }
                                //else
                                //{
                                //    if (service.RetrieveMultiple(new FetchExpression(fetch1)).Entities.Count > 0)
                                //    {
                                //        service.Delete("lux_applicationendorsements", service.RetrieveMultiple(new FetchExpression(fetch1)).Entities[0].Id);
                                //    }
                                //}
                            }

                            var Endorsement8 = service.RetrieveMultiple(new FetchExpression(endorsementFetch)).Entities.FirstOrDefault(x => x.Attributes["lux_name"].ToString() == "Increased Subsidence Excess - £5,000");
                            if (Endorsement8 != null)
                            {
                                var fetch1 = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                                                          <entity name='lux_applicationendorsements'>
                                                            <attribute name='lux_applicationendorsementsid' />
                                                            <attribute name='lux_name' />
                                                            <attribute name='lux_endorsementnumber' />
                                                            <attribute name='createdon' />
                                                            <order attribute='lux_name' descending='false' />
                                                            <filter type='and'>
                                                              <condition attribute='statecode' operator='eq' value='0' />
                                                              <condition attribute='lux_endorsementlibrary' operator='eq' uiname='' uitype='lux_endorsementlibrary' value='{Endorsement8.Attributes["lux_endorsementlibraryid"]}' />
                                                              <condition attribute='lux_application' operator='eq' uiname='' uitype='lux_propertyownersapplications' value='{appln.Id}' />
                                                            </filter>
                                                          </entity>
                                                        </fetch>";
                                if (SubsidienceScore == 6 && item.GetAttributeValue<bool>("lux_issubsidencecoverrequired") == true) //£750 Flood Excess
                                {
                                    if (service.RetrieveMultiple(new FetchExpression(fetch1)).Entities.Count == 0)
                                    {
                                        Entity ent = new Entity("lux_applicationendorsements");
                                        ent["lux_isdefault"] = true;
                                        ent["lux_endorsementnumber"] = Endorsement8.Attributes["new_endorsementnumber"];
                                        ent["lux_endorsementhtml"] = Endorsement8.Attributes["lux_endorsementhtml"];
                                        ent["lux_name"] = Endorsement8.Attributes["lux_name"];
                                        ent["lux_endorsementlibrary"] = new EntityReference("lux_endorsementlibrary", new Guid(Endorsement8.Attributes["lux_endorsementlibraryid"].ToString()));
                                        ent["lux_application"] = new EntityReference("lux_propertyownersapplications", appln.Id);
                                        service.Create(ent);
                                    }
                                }
                                //else
                                //{
                                //    if (service.RetrieveMultiple(new FetchExpression(fetch1)).Entities.Count > 0)
                                //    {
                                //        service.Delete("lux_applicationendorsements", service.RetrieveMultiple(new FetchExpression(fetch1)).Entities[0].Id);
                                //    }
                                //}
                            }

                            //if (model.IsLive == 0)
                            //{
                            var Endorsement9 = service.RetrieveMultiple(new FetchExpression(endorsementFetch)).Entities.FirstOrDefault(x => x.Attributes["lux_name"].ToString() == "Flat Roof - Increased Excess");
                            if (Endorsement9 != null)
                            {
                                var percent = item.Attributes.Contains("lux_flatroofpercentage") ? item.Attributes["lux_flatroofpercentage"].ToString().Replace("%", "") : "0";
                                percent = Regex.Match(percent, @"\d+").Value;

                                var fetch1 = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                                                              <entity name='lux_applicationendorsements'>
                                                                <attribute name='lux_applicationendorsementsid' />
                                                                <attribute name='lux_name' />
                                                                <attribute name='lux_endorsementnumber' />
                                                                <attribute name='createdon' />
                                                                <order attribute='lux_name' descending='false' />
                                                                <filter type='and'>
                                                                  <condition attribute='statecode' operator='eq' value='0' />
                                                                  <condition attribute='lux_endorsementlibrary' operator='eq' uiname='Wood Burner Condition' uitype='lux_endorsementlibrary' value='{Endorsement9.Attributes["lux_endorsementlibraryid"]}' />
                                                                  <condition attribute='lux_application' operator='eq' uiname='' uitype='lux_propertyownersapplications' value='{appln.Id}' />
                                                                </filter>
                                                              </entity>
                                                            </fetch>";

                                if (FlatRoof == true && Convert.ToDecimal(percent) > 50) // Flat roof
                                {
                                    if (service.RetrieveMultiple(new FetchExpression(fetch1)).Entities.Count == 0)
                                    {
                                        Entity ent = new Entity("lux_applicationendorsements");
                                        ent["lux_isdefault"] = true;
                                        ent["lux_endorsementhtml"] = Endorsement9.Attributes["lux_endorsementhtml"];
                                        ent["lux_endorsementnumber"] = Endorsement9.Attributes["new_endorsementnumber"];
                                        ent["lux_name"] = Endorsement9.Attributes["lux_name"];
                                        ent["lux_endorsementlibrary"] = new EntityReference("lux_endorsementlibrary", new Guid(Endorsement9.Attributes["lux_endorsementlibraryid"].ToString()));
                                        ent["lux_application"] = new EntityReference("lux_propertyownersapplications", appln.Id);
                                        service.Create(ent);
                                    }
                                }
                                //else
                                //{
                                //    if (service.RetrieveMultiple(new FetchExpression(fetch1)).Entities.Count > 0)
                                //    {
                                //        service.Delete("lux_applicationendorsements", service.RetrieveMultiple(new FetchExpression(fetch1)).Entities[0].Id);
                                //    }
                                //}
                            }
                            //}

                            var Endorsement10 = service.RetrieveMultiple(new FetchExpression(endorsementFetch)).Entities.FirstOrDefault(x => x.Attributes["lux_name"].ToString() == "Subsidence Exclusion");
                            if (Endorsement10 != null)
                            {
                                var fetch1 = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                                                          <entity name='lux_applicationendorsements'>
                                                            <attribute name='lux_applicationendorsementsid' />
                                                            <attribute name='lux_name' />
                                                            <attribute name='lux_endorsementnumber' />
                                                            <attribute name='createdon' />
                                                            <order attribute='lux_name' descending='false' />
                                                            <filter type='and'>
                                                              <condition attribute='statecode' operator='eq' value='0' />
                                                              <condition attribute='lux_endorsementlibrary' operator='eq' uiname='' uitype='lux_endorsementlibrary' value='{Endorsement10.Attributes["lux_endorsementlibraryid"]}' />
                                                              <condition attribute='lux_application' operator='eq' uiname='' uitype='lux_propertyownersapplications' value='{appln.Id}' />
                                                            </filter>
                                                          </entity>
                                                        </fetch>";
                                if (SubsidienceScore == 7 && item.GetAttributeValue<bool>("lux_issubsidencecoverrequired") == true) //£750 Flood Excess
                                {
                                    if (service.RetrieveMultiple(new FetchExpression(fetch1)).Entities.Count == 0)
                                    {
                                        Entity ent = new Entity("lux_applicationendorsements");
                                        ent["lux_isdefault"] = true;
                                        ent["lux_endorsementnumber"] = Endorsement10.Attributes["new_endorsementnumber"];
                                        ent["lux_endorsementhtml"] = Endorsement10.Attributes["lux_endorsementhtml"];
                                        ent["lux_name"] = Endorsement10.Attributes["lux_name"];
                                        ent["lux_endorsementlibrary"] = new EntityReference("lux_endorsementlibrary", new Guid(Endorsement10.Attributes["lux_endorsementlibraryid"].ToString()));
                                        ent["lux_application"] = new EntityReference("lux_propertyownersapplications", appln.Id);
                                        service.Create(ent);
                                    }
                                }
                                //else
                                //{
                                //    if (service.RetrieveMultiple(new FetchExpression(fetch1)).Entities.Count > 0)
                                //    {
                                //        service.Delete("lux_applicationendorsements", service.RetrieveMultiple(new FetchExpression(fetch1)).Entities[0].Id);
                                //    }
                                //}
                            }

                            var Endorsement11 = service.RetrieveMultiple(new FetchExpression(endorsementFetch)).Entities.FirstOrDefault(x => x.Attributes["lux_name"].ToString() == "Increased Flood Excess - £500");
                            if (Endorsement11 != null)
                            {
                                var fetch1 = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                                                          <entity name='lux_applicationendorsements'>
                                                            <attribute name='lux_applicationendorsementsid' />
                                                            <attribute name='lux_name' />
                                                            <attribute name='lux_endorsementnumber' />
                                                            <attribute name='createdon' />
                                                            <order attribute='lux_name' descending='false' />
                                                            <filter type='and'>
                                                              <condition attribute='statecode' operator='eq' value='0' />
                                                              <condition attribute='lux_endorsementlibrary' operator='eq' uiname='' uitype='lux_endorsementlibrary' value='{Endorsement11.Attributes["lux_endorsementlibraryid"]}' />
                                                              <condition attribute='lux_application' operator='eq' uiname='' uitype='lux_propertyownersapplications' value='{appln.Id}' />
                                                            </filter>
                                                          </entity>
                                                        </fetch>";
                                if (FloodScore == 9) //£500 Flood Excess
                                {
                                    if (service.RetrieveMultiple(new FetchExpression(fetch1)).Entities.Count == 0)
                                    {
                                        Entity ent = new Entity("lux_applicationendorsements");
                                        ent["lux_isdefault"] = true;
                                        ent["lux_endorsementnumber"] = Endorsement11.Attributes["new_endorsementnumber"];
                                        ent["lux_endorsementhtml"] = Endorsement11.Attributes["lux_endorsementhtml"];
                                        ent["lux_name"] = Endorsement11.Attributes["lux_name"];
                                        ent["lux_endorsementlibrary"] = new EntityReference("lux_endorsementlibrary", new Guid(Endorsement11.Attributes["lux_endorsementlibraryid"].ToString()));
                                        ent["lux_application"] = new EntityReference("lux_propertyownersapplications", appln.Id);
                                        service.Create(ent);
                                    }
                                }
                                //else
                                //{
                                //    if (service.RetrieveMultiple(new FetchExpression(fetch1)).Entities.Count > 0)
                                //    {
                                //        service.Delete("lux_applicationendorsements", service.RetrieveMultiple(new FetchExpression(fetch1)).Entities[0].Id);
                                //    }
                                //}
                            }

                            if (item.Attributes.Contains("lux_typeofheating"))
                            {
                                var Endorsement12 = service.RetrieveMultiple(new FetchExpression(endorsementFetch)).Entities.FirstOrDefault(x => x.Attributes["lux_name"].ToString() == "Wood Burner Condition");
                                if (Endorsement12 != null)
                                {
                                    var fetch1 = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                                                          <entity name='lux_applicationendorsements'>
                                                            <attribute name='lux_applicationendorsementsid' />
                                                            <attribute name='lux_name' />
                                                            <attribute name='lux_endorsementnumber' />
                                                            <attribute name='createdon' />
                                                            <order attribute='lux_name' descending='false' />
                                                            <filter type='and'>
                                                              <condition attribute='statecode' operator='eq' value='0' />
                                                              <condition attribute='lux_endorsementlibrary' operator='eq' uiname='' uitype='lux_endorsementlibrary' value='{Endorsement12.Attributes["lux_endorsementlibraryid"]}' />
                                                              <condition attribute='lux_application' operator='eq' uiname='' uitype='lux_propertyownersapplications' value='{appln.Id}' />
                                                            </filter>
                                                          </entity>
                                                        </fetch>";
                                    var HeatingType = item.GetAttributeValue<OptionSetValueCollection>("lux_typeofheating");
                                    if (HeatingType.Contains(new OptionSetValue(972970010)))
                                    {
                                        if (service.RetrieveMultiple(new FetchExpression(fetch1)).Entities.Count == 0)
                                        {
                                            Entity ent = new Entity("lux_applicationendorsements");
                                            ent["lux_isdefault"] = true;
                                            ent["lux_endorsementnumber"] = Endorsement12.Attributes["new_endorsementnumber"];
                                            ent["lux_endorsementhtml"] = Endorsement12.Attributes["lux_endorsementhtml"];
                                            ent["lux_name"] = Endorsement12.Attributes["lux_name"];
                                            ent["lux_endorsementlibrary"] = new EntityReference("lux_endorsementlibrary", new Guid(Endorsement12.Attributes["lux_endorsementlibraryid"].ToString()));
                                            ent["lux_application"] = new EntityReference("lux_propertyownersapplications", appln.Id);
                                            service.Create(ent);
                                        }
                                    }
                                    //else
                                    //{
                                    //    if (service.RetrieveMultiple(new FetchExpression(fetch1)).Entities.Count > 0)
                                    //    {
                                    //        service.Delete("lux_applicationendorsements", service.RetrieveMultiple(new FetchExpression(fetch1)).Entities[0].Id);
                                    //    }
                                    //}
                                }

                                var Endorsement17 = service.RetrieveMultiple(new FetchExpression(endorsementFetch)).Entities.FirstOrDefault(x => x.Attributes["lux_name"].ToString() == "Chimney Sweeping and Open fires");
                                if (Endorsement17 != null)
                                {
                                    var fetch1 = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                                                          <entity name='lux_applicationendorsements'>
                                                            <attribute name='lux_applicationendorsementsid' />
                                                            <attribute name='lux_name' />
                                                            <attribute name='lux_endorsementnumber' />
                                                            <attribute name='createdon' />
                                                            <order attribute='lux_name' descending='false' />
                                                            <filter type='and'>
                                                              <condition attribute='statecode' operator='eq' value='0' />
                                                              <condition attribute='lux_endorsementlibrary' operator='eq' uiname='' uitype='lux_endorsementlibrary' value='{Endorsement17.Attributes["lux_endorsementlibraryid"]}' />
                                                              <condition attribute='lux_application' operator='eq' uiname='' uitype='lux_propertyownersapplications' value='{appln.Id}' />
                                                            </filter>
                                                          </entity>
                                                        </fetch>";
                                    var HeatingType = item.GetAttributeValue<OptionSetValueCollection>("lux_typeofheating");
                                    if (HeatingType.Contains(new OptionSetValue(972970010)))
                                    {
                                        if (service.RetrieveMultiple(new FetchExpression(fetch1)).Entities.Count == 0)
                                        {
                                            Entity ent = new Entity("lux_applicationendorsements");
                                            ent["lux_isdefault"] = true;
                                            ent["lux_endorsementnumber"] = Endorsement17.Attributes["new_endorsementnumber"];
                                            ent["lux_endorsementhtml"] = Endorsement17.Attributes["lux_endorsementhtml"];
                                            ent["lux_name"] = Endorsement17.Attributes["lux_name"];
                                            ent["lux_endorsementlibrary"] = new EntityReference("lux_endorsementlibrary", new Guid(Endorsement17.Attributes["lux_endorsementlibraryid"].ToString()));
                                            ent["lux_application"] = new EntityReference("lux_propertyownersapplications", appln.Id);
                                            service.Create(ent);
                                        }
                                    }
                                    //else
                                    //{
                                    //    if (service.RetrieveMultiple(new FetchExpression(fetch1)).Entities.Count > 0)
                                    //    {
                                    //        service.Delete("lux_applicationendorsements", service.RetrieveMultiple(new FetchExpression(fetch1)).Entities[0].Id);
                                    //    }
                                    //}
                                }
                            }

                            var Endorsement13 = service.RetrieveMultiple(new FetchExpression(endorsementFetch)).Entities.FirstOrDefault(x => x.Attributes["lux_name"].ToString() == "Work Away Exclusion - Public and Products Liability");
                            if (Endorsement13 != null)
                            {
                                var fetch1 = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                                                          <entity name='lux_applicationendorsements'>
                                                            <attribute name='lux_applicationendorsementsid' />
                                                            <attribute name='lux_name' />
                                                            <attribute name='lux_endorsementnumber' />
                                                            <attribute name='createdon' />
                                                            <order attribute='lux_name' descending='false' />
                                                            <filter type='and'>
                                                              <condition attribute='statecode' operator='eq' value='0' />
                                                              <condition attribute='lux_endorsementlibrary' operator='eq' uiname='' uitype='lux_endorsementlibrary' value='{Endorsement13.Attributes["lux_endorsementlibraryid"]}' />
                                                              <condition attribute='lux_application' operator='eq' uiname='' uitype='lux_propertyownersapplications' value='{appln.Id}' />
                                                            </filter>
                                                          </entity>
                                                        </fetch>";

                                if (WorkAway == false)
                                {
                                    if (service.RetrieveMultiple(new FetchExpression(fetch1)).Entities.Count == 0)
                                    {
                                        Entity ent = new Entity("lux_applicationendorsements");
                                        ent["lux_isdefault"] = true;
                                        ent["lux_endorsementhtml"] = Endorsement13.Attributes.Contains("lux_endorsementhtml") ? Endorsement13.Attributes["lux_endorsementhtml"] : "";
                                        ent["lux_endorsementnumber"] = Endorsement13.Attributes["new_endorsementnumber"];
                                        ent["lux_name"] = Endorsement13.Attributes["lux_name"];
                                        ent["lux_endorsementlibrary"] = new EntityReference("lux_endorsementlibrary", new Guid(Endorsement13.Attributes["lux_endorsementlibraryid"].ToString()));
                                        ent["lux_application"] = new EntityReference("lux_propertyownersapplications", appln.Id);
                                        service.Create(ent);
                                    }
                                }
                                //else
                                //{
                                //    if (service.RetrieveMultiple(new FetchExpression(fetch1)).Entities.Count > 0)
                                //    {
                                //        service.Delete("lux_applicationendorsements", service.RetrieveMultiple(new FetchExpression(fetch1)).Entities[0].Id);
                                //    }
                                //}
                            }

                            var Endorsement14 = service.RetrieveMultiple(new FetchExpression(endorsementFetch)).Entities.FirstOrDefault(x => x.Attributes["lux_name"].ToString() == "Alarm Requirement: Audible Signalling");
                            if (Endorsement14 != null)
                            {
                                var fetch1 = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                                                          <entity name='lux_applicationendorsements'>
                                                            <attribute name='lux_applicationendorsementsid' />
                                                            <attribute name='lux_name' />
                                                            <attribute name='lux_endorsementnumber' />
                                                            <attribute name='createdon' />
                                                            <order attribute='lux_name' descending='false' />
                                                            <filter type='and'>
                                                              <condition attribute='statecode' operator='eq' value='0' />
                                                              <condition attribute='lux_endorsementlibrary' operator='eq' uiname='' uitype='lux_endorsementlibrary' value='{Endorsement14.Attributes["lux_endorsementlibraryid"]}' />
                                                              <condition attribute='lux_application' operator='eq' uiname='' uitype='lux_propertyownersapplications' value='{appln.Id}' />
                                                            </filter>
                                                          </entity>
                                                        </fetch>";

                                if (IntruderAlarm == true && TypeOFAlarm == 972970001)//audible only
                                {
                                    if (service.RetrieveMultiple(new FetchExpression(fetch1)).Entities.Count == 0)
                                    {
                                        Entity ent = new Entity("lux_applicationendorsements");
                                        ent["lux_isdefault"] = true;
                                        ent["lux_endorsementhtml"] = Endorsement14.Attributes.Contains("lux_endorsementhtml") ? Endorsement14.Attributes["lux_endorsementhtml"] : "";
                                        ent["lux_endorsementnumber"] = Endorsement14.Attributes["new_endorsementnumber"];
                                        ent["lux_name"] = Endorsement14.Attributes["lux_name"];
                                        ent["lux_endorsementlibrary"] = new EntityReference("lux_endorsementlibrary", new Guid(Endorsement14.Attributes["lux_endorsementlibraryid"].ToString()));
                                        ent["lux_application"] = new EntityReference("lux_propertyownersapplications", appln.Id);
                                        service.Create(ent);
                                    }
                                }
                                //else
                                //{
                                //    if (service.RetrieveMultiple(new FetchExpression(fetch1)).Entities.Count > 0)
                                //    {
                                //        service.Delete("lux_applicationendorsements", service.RetrieveMultiple(new FetchExpression(fetch1)).Entities[0].Id);
                                //    }
                                //}
                            }

                            var Endorsement15 = service.RetrieveMultiple(new FetchExpression(endorsementFetch)).Entities.FirstOrDefault(x => x.Attributes["lux_name"].ToString() == "Alarm Requirement: Non-Confirmed Signalling");
                            if (Endorsement15 != null)
                            {
                                var fetch1 = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                                                          <entity name='lux_applicationendorsements'>
                                                            <attribute name='lux_applicationendorsementsid' />
                                                            <attribute name='lux_name' />
                                                            <attribute name='lux_endorsementnumber' />
                                                            <attribute name='createdon' />
                                                            <order attribute='lux_name' descending='false' />
                                                            <filter type='and'>
                                                              <condition attribute='statecode' operator='eq' value='0' />
                                                              <condition attribute='lux_endorsementlibrary' operator='eq' uiname='' uitype='lux_endorsementlibrary' value='{Endorsement15.Attributes["lux_endorsementlibraryid"]}' />
                                                              <condition attribute='lux_application' operator='eq' uiname='' uitype='lux_propertyownersapplications' value='{appln.Id}' />
                                                            </filter>
                                                          </entity>
                                                        </fetch>";

                                if (IntruderAlarm == true && (TypeOFAlarm == 972970002 || TypeOFAlarm == 972970005))
                                {
                                    if (service.RetrieveMultiple(new FetchExpression(fetch1)).Entities.Count == 0)
                                    {
                                        Entity ent = new Entity("lux_applicationendorsements");
                                        ent["lux_isdefault"] = true;
                                        ent["lux_endorsementhtml"] = Endorsement15.Attributes.Contains("lux_endorsementhtml") ? Endorsement15.Attributes["lux_endorsementhtml"] : "";
                                        ent["lux_endorsementnumber"] = Endorsement15.Attributes["new_endorsementnumber"];
                                        ent["lux_name"] = Endorsement15.Attributes["lux_name"];
                                        ent["lux_endorsementlibrary"] = new EntityReference("lux_endorsementlibrary", new Guid(Endorsement15.Attributes["lux_endorsementlibraryid"].ToString()));
                                        ent["lux_application"] = new EntityReference("lux_propertyownersapplications", appln.Id);
                                        service.Create(ent);
                                    }
                                }
                                //else
                                //{
                                //    if (service.RetrieveMultiple(new FetchExpression(fetch1)).Entities.Count > 0)
                                //    {
                                //        service.Delete("lux_applicationendorsements", service.RetrieveMultiple(new FetchExpression(fetch1)).Entities[0].Id);
                                //    }
                                //}
                            }

                            var Endorsement16 = service.RetrieveMultiple(new FetchExpression(endorsementFetch)).Entities.FirstOrDefault(x => x.Attributes["lux_name"].ToString() == "Alarm Requirement: Confirmed Signalling");
                            if (Endorsement16 != null)
                            {
                                var fetch1 = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                                                          <entity name='lux_applicationendorsements'>
                                                            <attribute name='lux_applicationendorsementsid' />
                                                            <attribute name='lux_name' />
                                                            <attribute name='lux_endorsementnumber' />
                                                            <attribute name='createdon' />
                                                            <order attribute='lux_name' descending='false' />
                                                            <filter type='and'>
                                                              <condition attribute='statecode' operator='eq' value='0' />
                                                              <condition attribute='lux_endorsementlibrary' operator='eq' uiname='' uitype='lux_endorsementlibrary' value='{Endorsement16.Attributes["lux_endorsementlibraryid"]}' />
                                                              <condition attribute='lux_application' operator='eq' uiname='' uitype='lux_propertyownersapplications' value='{appln.Id}' />
                                                            </filter>
                                                          </entity>
                                                        </fetch>";

                                if (IntruderAlarm == true && (TypeOFAlarm == 972970003 || TypeOFAlarm == 972970004))//redcare gsm or dualcom
                                {
                                    if (service.RetrieveMultiple(new FetchExpression(fetch1)).Entities.Count == 0)
                                    {
                                        Entity ent = new Entity("lux_applicationendorsements");
                                        ent["lux_isdefault"] = true;
                                        ent["lux_endorsementhtml"] = Endorsement16.Attributes.Contains("lux_endorsementhtml") ? Endorsement16.Attributes["lux_endorsementhtml"] : "";
                                        ent["lux_endorsementnumber"] = Endorsement16.Attributes["new_endorsementnumber"];
                                        ent["lux_name"] = Endorsement16.Attributes["lux_name"];
                                        ent["lux_endorsementlibrary"] = new EntityReference("lux_endorsementlibrary", new Guid(Endorsement16.Attributes["lux_endorsementlibraryid"].ToString()));
                                        ent["lux_application"] = new EntityReference("lux_propertyownersapplications", appln.Id);
                                        service.Create(ent);
                                    }
                                }
                                //else
                                //{
                                //    if (service.RetrieveMultiple(new FetchExpression(fetch1)).Entities.Count > 0)
                                //    {
                                //        service.Delete("lux_applicationendorsements", service.RetrieveMultiple(new FetchExpression(fetch1)).Entities[0].Id);
                                //    }
                                //}
                            }

                            var Endorsement19 = service.RetrieveMultiple(new FetchExpression(endorsementFetch)).Entities.FirstOrDefault(x => x.Attributes["lux_name"].ToString() == "Work Away Exclusion - Employers Liability");
                            if (Endorsement19 != null)
                            {
                                var fetch1 = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                                                          <entity name='lux_applicationendorsements'>
                                                            <attribute name='lux_applicationendorsementsid' />
                                                            <attribute name='lux_name' />
                                                            <attribute name='lux_endorsementnumber' />
                                                            <attribute name='createdon' />
                                                            <order attribute='lux_name' descending='false' />
                                                            <filter type='and'>
                                                              <condition attribute='statecode' operator='eq' value='0' />
                                                              <condition attribute='lux_endorsementlibrary' operator='eq' uiname='' uitype='lux_endorsementlibrary' value='{Endorsement19.Attributes["lux_endorsementlibraryid"]}' />
                                                              <condition attribute='lux_application' operator='eq' uiname='' uitype='lux_propertyownersapplications' value='{appln.Id}' />
                                                            </filter>
                                                          </entity>
                                                        </fetch>";

                                if (WorkAway == false)
                                {
                                    if (service.RetrieveMultiple(new FetchExpression(fetch1)).Entities.Count == 0)
                                    {
                                        Entity ent = new Entity("lux_applicationendorsements");
                                        ent["lux_isdefault"] = true;
                                        ent["lux_endorsementhtml"] = Endorsement19.Attributes.Contains("lux_endorsementhtml") ? Endorsement19.Attributes["lux_endorsementhtml"] : "";
                                        ent["lux_endorsementnumber"] = Endorsement19.Attributes["new_endorsementnumber"];
                                        ent["lux_name"] = Endorsement19.Attributes["lux_name"];
                                        ent["lux_endorsementlibrary"] = new EntityReference("lux_endorsementlibrary", new Guid(Endorsement19.Attributes["lux_endorsementlibraryid"].ToString()));
                                        ent["lux_application"] = new EntityReference("lux_propertyownersapplications", appln.Id);
                                        service.Create(ent);
                                    }
                                }
                                //else
                                //{
                                //    if (service.RetrieveMultiple(new FetchExpression(fetch1)).Entities.Count > 0)
                                //    {
                                //        service.Delete("lux_applicationendorsements", service.RetrieveMultiple(new FetchExpression(fetch1)).Entities[0].Id);
                                //    }
                                //}
                            }

                            var Endorsement20 = service.RetrieveMultiple(new FetchExpression(endorsementFetch)).Entities.FirstOrDefault(x => x.Attributes["lux_name"].ToString() == "Products Exclusion");
                            if (Endorsement20 != null)
                            {
                                var fetch1 = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                                                          <entity name='lux_applicationendorsements'>
                                                            <attribute name='lux_applicationendorsementsid' />
                                                            <attribute name='lux_name' />
                                                            <attribute name='lux_endorsementnumber' />
                                                            <attribute name='createdon' />
                                                            <order attribute='lux_name' descending='false' />
                                                            <filter type='and'>
                                                              <condition attribute='statecode' operator='eq' value='0' />
                                                              <condition attribute='lux_endorsementlibrary' operator='eq' uiname='' uitype='lux_endorsementlibrary' value='{Endorsement20.Attributes["lux_endorsementlibraryid"]}' />
                                                              <condition attribute='lux_application' operator='eq' uiname='' uitype='lux_propertyownersapplications' value='{appln.Id}' />
                                                            </filter>
                                                          </entity>
                                                        </fetch>";

                                if (ProdusHazardGrade >= 6)
                                {
                                    if (service.RetrieveMultiple(new FetchExpression(fetch1)).Entities.Count == 0)
                                    {
                                        Entity ent = new Entity("lux_applicationendorsements");
                                        ent["lux_isdefault"] = true;
                                        ent["lux_endorsementnumber"] = Endorsement20.Attributes["new_endorsementnumber"];
                                        ent["lux_endorsementhtml"] = Endorsement20.Attributes["lux_endorsementhtml"];
                                        ent["lux_name"] = Endorsement20.Attributes["lux_name"];
                                        ent["lux_endorsementlibrary"] = new EntityReference("lux_endorsementlibrary", new Guid(Endorsement20.Attributes["lux_endorsementlibraryid"].ToString()));
                                        ent["lux_application"] = new EntityReference("lux_propertyownersapplications", appln.Id);
                                        service.Create(ent);
                                    }
                                }
                                //else
                                //{
                                //    if (service.RetrieveMultiple(new FetchExpression(fetch1)).Entities.Count > 0)
                                //    {
                                //        service.Delete("lux_applicationendorsements", service.RetrieveMultiple(new FetchExpression(fetch1)).Entities[0].Id);
                                //    }
                                //}
                            }
                        }
                    }
                }
            }
            else if (productName == "Contractors Combined")
            {
                var fetch = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                                      <entity name='lux_contractorscombined'>
                                        <attribute name='lux_contractorscombinedid' />
                                        <attribute name='lux_name' />
                                        <attribute name='createdon' />
                                        <attribute name='lux_rentreceivable' />
                                        <attribute name='lux_icow' />
                                        <attribute name='lux_typeofcover' />
                                        <attribute name='lux_bookdebts' />
                                        <attribute name='lux_amount' />
                                        <attribute name='lux_issubsidencecoverrequired' />
                                        <attribute name='lux_doesthepremiseshaveaflatroof' />
                                        <attribute name='lux_flatroofpercentage' />
                                        <attribute name='lux_doesthepremiseshaveabasement' />
                                        <attribute name='lux_isthereanatm' />
                                        <attribute name='lux_isthereanintruderalarminstalledandinworki' />                                
                                        <attribute name='lux_whattypeofalarmisinstalled' />
                                        <attribute name='lux_isownerresponsibleforfillingandemptyingth' />
                                        <attribute name='lux_isthereanydeepfatfrying' />
                                        <attribute name='lux_additionalincreasedcostofworking' />
                                        <attribute name='lux_floodscore' />
                                        <attribute name='lux_subsidencescore' />
                                        <attribute name='lux_crimescore' />
                                        <attribute name='lux_securityrating' />
                                        <order attribute='lux_name' descending='false' />
                                        <filter type='and'>
                                          <condition attribute='lux_propertyownersapplications' operator='eq' uiname='' uitype='lux_propertyownersapplications' value='{appln.Id}' />
                                        </filter>
                                        <link-entity name='lux_propertyownersapplications' from='lux_propertyownersapplicationsid' to='lux_propertyownersapplications' visible='false' link-type='outer' alias='appln'>
                                          <attribute name='lux_anyheatworkawayundertaken' />
                                          <attribute name='lux_isanyworkawaycarriedoutotherthanforcollec' />
                                        </link-entity>
                                      </entity>
                                    </fetch>";

                if (service.RetrieveMultiple(new FetchExpression(fetch)).Entities.Count > 0)
                {
                    var endorsementFetch = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                                                          <entity name='lux_endorsementlibrary'>
                                                            <attribute name='lux_name' />
                                                            <attribute name='new_product' />
                                                            <attribute name='lux_insurer' />
                                                            <attribute name='lux_endorsementdescription' />
                                                            <attribute name='lux_endorsementhtml' />
                                                            <attribute name='new_endorsementnumber' />
                                                            <attribute name='lux_endorsementlibraryid' />
                                                            <order attribute='lux_name' descending='false' />
                                                            <filter type='and'>
                                                              <condition attribute='statecode' operator='eq' value='0' />
                                                              <condition attribute='lux_name' operator='not-null' />
                                                              <filter type='or'>
                                                                <condition attribute='new_product' operator='eq' uiname='' uitype='product' value='{"1ca35a2b-23a4-eb11-b1ac-002248404342"}' />
                                                                <condition attribute='new_product' operator='null' />
                                                              </filter>
                                                            </filter>
                                                          </entity>
                                                        </fetch>";

                    foreach (var item in service.RetrieveMultiple(new FetchExpression(fetch)).Entities)
                    {
                        var FlatRoof = item.Attributes.Contains("lux_doesthepremiseshaveaflatroof") ? item.GetAttributeValue<bool>("lux_doesthepremiseshaveaflatroof") : false;
                        var ATM = item.Attributes.Contains("lux_isthereanatm") ? item.GetAttributeValue<bool>("lux_isthereanatm") : false;
                        var OwnerResponsible = item.Attributes.Contains("lux_isownerresponsibleforfillingandemptyingth") ? item.GetAttributeValue<bool>("lux_isownerresponsibleforfillingandemptyingth") : false;
                        var AnyDFF = item.Attributes.Contains("lux_isthereanydeepfatfrying") ? item.GetAttributeValue<bool>("lux_isthereanydeepfatfrying") : false;
                        var AnyHeatWorkAway = item.Attributes.Contains("appln.lux_anyheatworkawayundertaken") ? ((bool)(item.GetAttributeValue<AliasedValue>("appln.lux_anyheatworkawayundertaken").Value)) : false;
                        var WorkAway = item.Attributes.Contains("appln.lux_isanyworkawaycarriedoutotherthanforcollec") ? ((bool)(item.GetAttributeValue<AliasedValue>("appln.lux_isanyworkawaycarriedoutotherthanforcollec").Value)) : false;
                        var IntruderAlarm = item.Attributes.Contains("lux_isthereanintruderalarminstalledandinworki") ? item.GetAttributeValue<bool>("lux_isthereanintruderalarminstalledandinworki") : false;
                        var TypeOFAlarm = item.Attributes.Contains("lux_whattypeofalarmisinstalled") ? item.GetAttributeValue<OptionSetValue>("lux_whattypeofalarmisinstalled").Value : 0;

                        if (service.RetrieveMultiple(new FetchExpression(endorsementFetch)).Entities.Count > 0)
                        {
                            var Endorsement1 = new Entity();
                            if (OwnerResponsible == true)
                            {
                                Endorsement1 = service.RetrieveMultiple(new FetchExpression(endorsementFetch)).Entities.FirstOrDefault(x => x.Attributes["lux_name"].ToString() == "Automated Teller Machine (ATM) - Self Fill");
                            }
                            else
                            {
                                Endorsement1 = service.RetrieveMultiple(new FetchExpression(endorsementFetch)).Entities.FirstOrDefault(x => x.Attributes["lux_name"].ToString() == "Automated Teller Machine (ATM) - Third Party Provided & Maintained Machines");
                            }

                            if (Endorsement1 != null)
                            {
                                var fetch1 = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                                                          <entity name='lux_applicationendorsements'>
                                                            <attribute name='lux_applicationendorsementsid' />
                                                            <attribute name='lux_name' />
                                                            <attribute name='lux_endorsementnumber' />
                                                            <attribute name='createdon' />
                                                            <order attribute='lux_name' descending='false' />
                                                            <filter type='and'>
                                                              <condition attribute='statecode' operator='eq' value='0' />
                                                              <condition attribute='lux_endorsementlibrary' operator='eq' uiname='' uitype='lux_endorsementlibrary' value='{Endorsement1.Attributes["lux_endorsementlibraryid"]}' />
                                                              <condition attribute='lux_application' operator='eq' uiname='' uitype='lux_propertyownersapplications' value='{appln.Id}' />
                                                            </filter>
                                                          </entity>
                                                        </fetch>";

                                if (ATM == true) // Flat roof
                                {
                                    if (service.RetrieveMultiple(new FetchExpression(fetch1)).Entities.Count == 0)
                                    {
                                        Entity ent = new Entity("lux_applicationendorsements");
                                        ent["lux_isdefault"] = true;
                                        ent["lux_endorsementhtml"] = Endorsement1.Attributes.Contains("lux_endorsementhtml") ? Endorsement1.Attributes["lux_endorsementhtml"] : "";
                                        ent["lux_endorsementnumber"] = Endorsement1.Attributes["new_endorsementnumber"];
                                        ent["lux_name"] = Endorsement1.Attributes["lux_name"];
                                        ent["lux_endorsementlibrary"] = new EntityReference("lux_endorsementlibrary", new Guid(Endorsement1.Attributes["lux_endorsementlibraryid"].ToString()));
                                        ent["lux_application"] = new EntityReference("lux_propertyownersapplications", appln.Id);
                                        service.Create(ent);
                                    }
                                }
                                //else
                                //{
                                //    if (service.RetrieveMultiple(new FetchExpression(fetch1)).Entities.Count > 0)
                                //    {
                                //        service.Delete("lux_applicationendorsements", service.RetrieveMultiple(new FetchExpression(fetch1)).Entities[0].Id);
                                //    }
                                //}
                            }

                            var Endorsement2 = service.RetrieveMultiple(new FetchExpression(endorsementFetch)).Entities.FirstOrDefault(x => x.Attributes["lux_name"].ToString() == "Kitchen Precautions");
                            if (Endorsement2 != null)
                            {
                                var fetch1 = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                                                          <entity name='lux_applicationendorsements'>
                                                            <attribute name='lux_applicationendorsementsid' />
                                                            <attribute name='lux_name' />
                                                            <attribute name='lux_endorsementnumber' />
                                                            <attribute name='createdon' />
                                                            <order attribute='lux_name' descending='false' />
                                                            <filter type='and'>
                                                              <condition attribute='statecode' operator='eq' value='0' />
                                                              <condition attribute='lux_endorsementlibrary' operator='eq' uiname='' uitype='lux_endorsementlibrary' value='{Endorsement2.Attributes["lux_endorsementlibraryid"]}' />
                                                              <condition attribute='lux_application' operator='eq' uiname='' uitype='lux_propertyownersapplications' value='{appln.Id}' />
                                                            </filter>
                                                          </entity>
                                                        </fetch>";

                                if (AnyDFF == true) // Flat roof
                                {
                                    if (service.RetrieveMultiple(new FetchExpression(fetch1)).Entities.Count == 0)
                                    {
                                        Entity ent = new Entity("lux_applicationendorsements");
                                        ent["lux_isdefault"] = true;
                                        ent["lux_endorsementhtml"] = Endorsement2.Attributes.Contains("lux_endorsementhtml") ? Endorsement2.Attributes["lux_endorsementhtml"] : "";
                                        ent["lux_endorsementnumber"] = Endorsement2.Attributes["new_endorsementnumber"];
                                        ent["lux_name"] = Endorsement2.Attributes["lux_name"];
                                        ent["lux_endorsementlibrary"] = new EntityReference("lux_endorsementlibrary", new Guid(Endorsement2.Attributes["lux_endorsementlibraryid"].ToString()));
                                        ent["lux_application"] = new EntityReference("lux_propertyownersapplications", appln.Id);
                                        service.Create(ent);
                                    }
                                }
                                else
                                {
                                    if (service.RetrieveMultiple(new FetchExpression(fetch1)).Entities.Count > 0)
                                    {
                                        service.Delete("lux_applicationendorsements", service.RetrieveMultiple(new FetchExpression(fetch1)).Entities[0].Id);
                                    }
                                }
                            }

                            var Endorsement3 = service.RetrieveMultiple(new FetchExpression(endorsementFetch)).Entities.FirstOrDefault(x => x.Attributes["lux_name"].ToString() == "Roof Maintenance Condition");
                            if (Endorsement3 != null)
                            {
                                var fetch1 = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                                                          <entity name='lux_applicationendorsements'>
                                                            <attribute name='lux_applicationendorsementsid' />
                                                            <attribute name='lux_name' />
                                                            <attribute name='lux_endorsementnumber' />
                                                            <attribute name='createdon' />
                                                            <order attribute='lux_name' descending='false' />
                                                            <filter type='and'>
                                                              <condition attribute='statecode' operator='eq' value='0' />
                                                              <condition attribute='lux_endorsementlibrary' operator='eq' uiname='' uitype='lux_endorsementlibrary' value='{Endorsement3.Attributes["lux_endorsementlibraryid"]}' />
                                                              <condition attribute='lux_application' operator='eq' uiname='' uitype='lux_propertyownersapplications' value='{appln.Id}' />
                                                            </filter>
                                                          </entity>
                                                        </fetch>";

                                if (FlatRoof == true) // Flat roof
                                {
                                    if (service.RetrieveMultiple(new FetchExpression(fetch1)).Entities.Count == 0)
                                    {
                                        Entity ent = new Entity("lux_applicationendorsements");
                                        ent["lux_isdefault"] = true;
                                        ent["lux_endorsementhtml"] = Endorsement3.Attributes.Contains("lux_endorsementhtml") ? Endorsement3.Attributes["lux_endorsementhtml"] : "";
                                        ent["lux_endorsementnumber"] = Endorsement3.Attributes["new_endorsementnumber"];
                                        ent["lux_name"] = Endorsement3.Attributes["lux_name"];
                                        ent["lux_endorsementlibrary"] = new EntityReference("lux_endorsementlibrary", new Guid(Endorsement3.Attributes["lux_endorsementlibraryid"].ToString()));
                                        ent["lux_application"] = new EntityReference("lux_propertyownersapplications", appln.Id);
                                        service.Create(ent);
                                    }
                                }
                                else
                                {
                                    if (service.RetrieveMultiple(new FetchExpression(fetch1)).Entities.Count > 0)
                                    {
                                        service.Delete("lux_applicationendorsements", service.RetrieveMultiple(new FetchExpression(fetch1)).Entities[0].Id);
                                    }
                                }
                            }

                            var Endorsement4 = service.RetrieveMultiple(new FetchExpression(endorsementFetch)).Entities.FirstOrDefault(x => x.Attributes["lux_name"].ToString() == "Heat Application Condition");
                            if (Endorsement4 != null)
                            {
                                var fetch1 = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                                                          <entity name='lux_applicationendorsements'>
                                                            <attribute name='lux_applicationendorsementsid' />
                                                            <attribute name='lux_name' />
                                                            <attribute name='lux_endorsementnumber' />
                                                            <attribute name='createdon' />
                                                            <order attribute='lux_name' descending='false' />
                                                            <filter type='and'>
                                                              <condition attribute='statecode' operator='eq' value='0' />
                                                              <condition attribute='lux_endorsementlibrary' operator='eq' uiname='' uitype='lux_endorsementlibrary' value='{Endorsement4.Attributes["lux_endorsementlibraryid"]}' />
                                                              <condition attribute='lux_application' operator='eq' uiname='' uitype='lux_propertyownersapplications' value='{appln.Id}' />
                                                            </filter>
                                                          </entity>
                                                        </fetch>";

                                if (AnyHeatWorkAway == true)
                                {
                                    if (service.RetrieveMultiple(new FetchExpression(fetch1)).Entities.Count == 0)
                                    {
                                        Entity ent = new Entity("lux_applicationendorsements");
                                        ent["lux_isdefault"] = true;
                                        ent["lux_endorsementhtml"] = Endorsement4.Attributes.Contains("lux_endorsementhtml") ? Endorsement4.Attributes["lux_endorsementhtml"] : "";
                                        ent["lux_endorsementnumber"] = Endorsement4.Attributes["new_endorsementnumber"];
                                        ent["lux_name"] = Endorsement4.Attributes["lux_name"];
                                        ent["lux_endorsementlibrary"] = new EntityReference("lux_endorsementlibrary", new Guid(Endorsement4.Attributes["lux_endorsementlibraryid"].ToString()));
                                        ent["lux_application"] = new EntityReference("lux_propertyownersapplications", appln.Id);
                                        service.Create(ent);
                                    }
                                }
                                else
                                {
                                    if (service.RetrieveMultiple(new FetchExpression(fetch1)).Entities.Count > 0)
                                    {
                                        service.Delete("lux_applicationendorsements", service.RetrieveMultiple(new FetchExpression(fetch1)).Entities[0].Id);
                                    }
                                }
                            }

                            var FloodScore = item.Attributes.Contains("lux_floodscore") ? item.GetAttributeValue<int>("lux_floodscore") : 0;
                            var SubsidienceScore = item.Attributes.Contains("lux_subsidencescore") ? item.GetAttributeValue<int>("lux_subsidencescore") : 0;

                            var Endorsement5 = service.RetrieveMultiple(new FetchExpression(endorsementFetch)).Entities.FirstOrDefault(x => x.Attributes["lux_name"].ToString() == "Increased Flood Excess - £1,000");
                            if (Endorsement5 != null)
                            {
                                var fetch1 = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                                                          <entity name='lux_applicationendorsements'>
                                                            <attribute name='lux_applicationendorsementsid' />
                                                            <attribute name='lux_name' />
                                                            <attribute name='lux_endorsementnumber' />
                                                            <attribute name='createdon' />
                                                            <order attribute='lux_name' descending='false' />
                                                            <filter type='and'>
                                                              <condition attribute='statecode' operator='eq' value='0' />
                                                              <condition attribute='lux_endorsementlibrary' operator='eq' uiname='' uitype='lux_endorsementlibrary' value='{Endorsement5.Attributes["lux_endorsementlibraryid"]}' />
                                                              <condition attribute='lux_application' operator='eq' uiname='' uitype='lux_propertyownersapplications' value='{appln.Id}' />
                                                            </filter>
                                                          </entity>
                                                        </fetch>";
                                if (FloodScore == 10) //£1000 Flood Excess
                                {
                                    if (service.RetrieveMultiple(new FetchExpression(fetch1)).Entities.Count == 0)
                                    {
                                        Entity ent = new Entity("lux_applicationendorsements");
                                        ent["lux_isdefault"] = true;
                                        ent["lux_endorsementnumber"] = Endorsement5.Attributes["new_endorsementnumber"];
                                        ent["lux_endorsementhtml"] = Endorsement5.Attributes["lux_endorsementhtml"];
                                        ent["lux_name"] = Endorsement5.Attributes["lux_name"];
                                        ent["lux_endorsementlibrary"] = new EntityReference("lux_endorsementlibrary", new Guid(Endorsement5.Attributes["lux_endorsementlibraryid"].ToString()));
                                        ent["lux_application"] = new EntityReference("lux_propertyownersapplications", appln.Id);
                                        service.Create(ent);
                                    }
                                }
                                else
                                {
                                    if (service.RetrieveMultiple(new FetchExpression(fetch1)).Entities.Count > 0)
                                    {
                                        service.Delete("lux_applicationendorsements", service.RetrieveMultiple(new FetchExpression(fetch1)).Entities[0].Id);
                                    }
                                }
                            }

                            var Endorsement6 = service.RetrieveMultiple(new FetchExpression(endorsementFetch)).Entities.FirstOrDefault(x => x.Attributes["lux_name"].ToString() == "Flood Exclusion");
                            if (Endorsement6 != null)
                            {
                                var fetch1 = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                                                          <entity name='lux_applicationendorsements'>
                                                            <attribute name='lux_applicationendorsementsid' />
                                                            <attribute name='lux_name' />
                                                            <attribute name='lux_endorsementnumber' />
                                                            <attribute name='createdon' />
                                                            <order attribute='lux_name' descending='false' />
                                                            <filter type='and'>
                                                              <condition attribute='statecode' operator='eq' value='0' />
                                                              <condition attribute='lux_endorsementlibrary' operator='eq' uiname='' uitype='lux_endorsementlibrary' value='{Endorsement6.Attributes["lux_endorsementlibraryid"]}' />
                                                              <condition attribute='lux_application' operator='eq' uiname='' uitype='lux_propertyownersapplications' value='{appln.Id}' />
                                                            </filter>
                                                          </entity>
                                                        </fetch>";
                                if (FloodScore >= 11) //Decline - Exclude Flood Cover
                                {
                                    if (service.RetrieveMultiple(new FetchExpression(fetch1)).Entities.Count == 0)
                                    {
                                        Entity ent = new Entity("lux_applicationendorsements");
                                        ent["lux_isdefault"] = true;
                                        ent["lux_endorsementnumber"] = Endorsement6.Attributes["new_endorsementnumber"];
                                        ent["lux_endorsementhtml"] = Endorsement6.Attributes["lux_endorsementhtml"];
                                        ent["lux_name"] = Endorsement6.Attributes["lux_name"];
                                        ent["lux_endorsementlibrary"] = new EntityReference("lux_endorsementlibrary", new Guid(Endorsement6.Attributes["lux_endorsementlibraryid"].ToString()));
                                        ent["lux_application"] = new EntityReference("lux_propertyownersapplications", appln.Id);
                                        service.Create(ent);
                                    }
                                }
                                else
                                {
                                    if (service.RetrieveMultiple(new FetchExpression(fetch1)).Entities.Count > 0)
                                    {
                                        service.Delete("lux_applicationendorsements", service.RetrieveMultiple(new FetchExpression(fetch1)).Entities[0].Id);
                                    }
                                }
                            }

                            var Endorsement7 = service.RetrieveMultiple(new FetchExpression(endorsementFetch)).Entities.FirstOrDefault(x => x.Attributes["lux_name"].ToString() == "Increased Subsidence Excess - £2,500");
                            if (Endorsement7 != null)
                            {
                                var fetch1 = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                                                          <entity name='lux_applicationendorsements'>
                                                            <attribute name='lux_applicationendorsementsid' />
                                                            <attribute name='lux_name' />
                                                            <attribute name='lux_endorsementnumber' />
                                                            <attribute name='createdon' />
                                                            <order attribute='lux_name' descending='false' />
                                                            <filter type='and'>
                                                              <condition attribute='statecode' operator='eq' value='0' />
                                                              <condition attribute='lux_endorsementlibrary' operator='eq' uiname='' uitype='lux_endorsementlibrary' value='{Endorsement7.Attributes["lux_endorsementlibraryid"]}' />
                                                              <condition attribute='lux_application' operator='eq' uiname='' uitype='lux_propertyownersapplications' value='{appln.Id}' />
                                                            </filter>
                                                          </entity>
                                                        </fetch>";
                                if ((SubsidienceScore == 4 || SubsidienceScore == 5) && item.GetAttributeValue<bool>("lux_issubsidencecoverrequired") == true) //£750 Flood Excess
                                {
                                    if (service.RetrieveMultiple(new FetchExpression(fetch1)).Entities.Count == 0)
                                    {
                                        Entity ent = new Entity("lux_applicationendorsements");
                                        ent["lux_isdefault"] = true;
                                        ent["lux_endorsementnumber"] = Endorsement7.Attributes["new_endorsementnumber"];
                                        ent["lux_endorsementhtml"] = Endorsement7.Attributes["lux_endorsementhtml"];
                                        ent["lux_name"] = Endorsement7.Attributes["lux_name"];
                                        ent["lux_endorsementlibrary"] = new EntityReference("lux_endorsementlibrary", new Guid(Endorsement7.Attributes["lux_endorsementlibraryid"].ToString()));
                                        ent["lux_application"] = new EntityReference("lux_propertyownersapplications", appln.Id);
                                        service.Create(ent);
                                    }
                                }
                                else
                                {
                                    if (service.RetrieveMultiple(new FetchExpression(fetch1)).Entities.Count > 0)
                                    {
                                        service.Delete("lux_applicationendorsements", service.RetrieveMultiple(new FetchExpression(fetch1)).Entities[0].Id);
                                    }
                                }
                            }

                            var Endorsement8 = service.RetrieveMultiple(new FetchExpression(endorsementFetch)).Entities.FirstOrDefault(x => x.Attributes["lux_name"].ToString() == "Increased Subsidence Excess - £5,000");
                            if (Endorsement8 != null)
                            {
                                var fetch1 = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                                                          <entity name='lux_applicationendorsements'>
                                                            <attribute name='lux_applicationendorsementsid' />
                                                            <attribute name='lux_name' />
                                                            <attribute name='lux_endorsementnumber' />
                                                            <attribute name='createdon' />
                                                            <order attribute='lux_name' descending='false' />
                                                            <filter type='and'>
                                                              <condition attribute='statecode' operator='eq' value='0' />
                                                              <condition attribute='lux_endorsementlibrary' operator='eq' uiname='' uitype='lux_endorsementlibrary' value='{Endorsement8.Attributes["lux_endorsementlibraryid"]}' />
                                                              <condition attribute='lux_application' operator='eq' uiname='' uitype='lux_propertyownersapplications' value='{appln.Id}' />
                                                            </filter>
                                                          </entity>
                                                        </fetch>";
                                if (SubsidienceScore == 6 && item.GetAttributeValue<bool>("lux_issubsidencecoverrequired") == true) //£750 Flood Excess
                                {
                                    if (service.RetrieveMultiple(new FetchExpression(fetch1)).Entities.Count == 0)
                                    {
                                        Entity ent = new Entity("lux_applicationendorsements");
                                        ent["lux_isdefault"] = true;
                                        ent["lux_endorsementnumber"] = Endorsement8.Attributes["new_endorsementnumber"];
                                        ent["lux_endorsementhtml"] = Endorsement8.Attributes["lux_endorsementhtml"];
                                        ent["lux_name"] = Endorsement8.Attributes["lux_name"];
                                        ent["lux_endorsementlibrary"] = new EntityReference("lux_endorsementlibrary", new Guid(Endorsement8.Attributes["lux_endorsementlibraryid"].ToString()));
                                        ent["lux_application"] = new EntityReference("lux_propertyownersapplications", appln.Id);
                                        service.Create(ent);
                                    }
                                }
                                else
                                {
                                    if (service.RetrieveMultiple(new FetchExpression(fetch1)).Entities.Count > 0)
                                    {
                                        service.Delete("lux_applicationendorsements", service.RetrieveMultiple(new FetchExpression(fetch1)).Entities[0].Id);
                                    }
                                }
                            }

                            var Endorsement10 = service.RetrieveMultiple(new FetchExpression(endorsementFetch)).Entities.FirstOrDefault(x => x.Attributes["lux_name"].ToString() == "Subsidence Exclusion");
                            if (Endorsement10 != null)
                            {
                                var fetch1 = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                                                          <entity name='lux_applicationendorsements'>
                                                            <attribute name='lux_applicationendorsementsid' />
                                                            <attribute name='lux_name' />
                                                            <attribute name='lux_endorsementnumber' />
                                                            <attribute name='createdon' />
                                                            <order attribute='lux_name' descending='false' />
                                                            <filter type='and'>
                                                              <condition attribute='statecode' operator='eq' value='0' />
                                                              <condition attribute='lux_endorsementlibrary' operator='eq' uiname='' uitype='lux_endorsementlibrary' value='{Endorsement10.Attributes["lux_endorsementlibraryid"]}' />
                                                              <condition attribute='lux_application' operator='eq' uiname='' uitype='lux_propertyownersapplications' value='{appln.Id}' />
                                                            </filter>
                                                          </entity>
                                                        </fetch>";
                                if (SubsidienceScore == 7 && item.GetAttributeValue<bool>("lux_issubsidencecoverrequired") == true) //£750 Flood Excess
                                {
                                    if (service.RetrieveMultiple(new FetchExpression(fetch1)).Entities.Count == 0)
                                    {
                                        Entity ent = new Entity("lux_applicationendorsements");
                                        ent["lux_isdefault"] = true;
                                        ent["lux_endorsementnumber"] = Endorsement10.Attributes["new_endorsementnumber"];
                                        ent["lux_endorsementhtml"] = Endorsement10.Attributes["lux_endorsementhtml"];
                                        ent["lux_name"] = Endorsement10.Attributes["lux_name"];
                                        ent["lux_endorsementlibrary"] = new EntityReference("lux_endorsementlibrary", new Guid(Endorsement10.Attributes["lux_endorsementlibraryid"].ToString()));
                                        ent["lux_application"] = new EntityReference("lux_propertyownersapplications", appln.Id);
                                        service.Create(ent);
                                    }
                                }
                                else
                                {
                                    if (service.RetrieveMultiple(new FetchExpression(fetch1)).Entities.Count > 0)
                                    {
                                        service.Delete("lux_applicationendorsements", service.RetrieveMultiple(new FetchExpression(fetch1)).Entities[0].Id);
                                    }
                                }
                            }

                            var Endorsement11 = service.RetrieveMultiple(new FetchExpression(endorsementFetch)).Entities.FirstOrDefault(x => x.Attributes["lux_name"].ToString() == "Increased Flood Excess - £500");
                            if (Endorsement11 != null)
                            {
                                var fetch1 = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                                                          <entity name='lux_applicationendorsements'>
                                                            <attribute name='lux_applicationendorsementsid' />
                                                            <attribute name='lux_name' />
                                                            <attribute name='lux_endorsementnumber' />
                                                            <attribute name='createdon' />
                                                            <order attribute='lux_name' descending='false' />
                                                            <filter type='and'>
                                                              <condition attribute='statecode' operator='eq' value='0' />
                                                              <condition attribute='lux_endorsementlibrary' operator='eq' uiname='' uitype='lux_endorsementlibrary' value='{Endorsement11.Attributes["lux_endorsementlibraryid"]}' />
                                                              <condition attribute='lux_application' operator='eq' uiname='' uitype='lux_propertyownersapplications' value='{appln.Id}' />
                                                            </filter>
                                                          </entity>
                                                        </fetch>";
                                if (FloodScore == 9) //£500 Flood Excess
                                {
                                    if (service.RetrieveMultiple(new FetchExpression(fetch1)).Entities.Count == 0)
                                    {
                                        Entity ent = new Entity("lux_applicationendorsements");
                                        ent["lux_isdefault"] = true;
                                        ent["lux_endorsementnumber"] = Endorsement11.Attributes["new_endorsementnumber"];
                                        ent["lux_endorsementhtml"] = Endorsement11.Attributes["lux_endorsementhtml"];
                                        ent["lux_name"] = Endorsement11.Attributes["lux_name"];
                                        ent["lux_endorsementlibrary"] = new EntityReference("lux_endorsementlibrary", new Guid(Endorsement11.Attributes["lux_endorsementlibraryid"].ToString()));
                                        ent["lux_application"] = new EntityReference("lux_propertyownersapplications", appln.Id);
                                        service.Create(ent);
                                    }
                                }
                                else
                                {
                                    if (service.RetrieveMultiple(new FetchExpression(fetch1)).Entities.Count > 0)
                                    {
                                        service.Delete("lux_applicationendorsements", service.RetrieveMultiple(new FetchExpression(fetch1)).Entities[0].Id);
                                    }
                                }
                            }

                            var Endorsement12 = service.RetrieveMultiple(new FetchExpression(endorsementFetch)).Entities.FirstOrDefault(x => x.Attributes["lux_name"].ToString() == "Work Away Exclusion - Employers Liability");
                            if (Endorsement12 != null)
                            {
                                var fetch1 = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                                                          <entity name='lux_applicationendorsements'>
                                                            <attribute name='lux_applicationendorsementsid' />
                                                            <attribute name='lux_name' />
                                                            <attribute name='lux_endorsementnumber' />
                                                            <attribute name='createdon' />
                                                            <order attribute='lux_name' descending='false' />
                                                            <filter type='and'>
                                                              <condition attribute='statecode' operator='eq' value='0' />
                                                              <condition attribute='lux_endorsementlibrary' operator='eq' uiname='' uitype='lux_endorsementlibrary' value='{Endorsement12.Attributes["lux_endorsementlibraryid"]}' />
                                                              <condition attribute='lux_application' operator='eq' uiname='' uitype='lux_propertyownersapplications' value='{appln.Id}' />
                                                            </filter>
                                                          </entity>
                                                        </fetch>";

                                if (WorkAway == false)
                                {
                                    if (service.RetrieveMultiple(new FetchExpression(fetch1)).Entities.Count == 0)
                                    {
                                        Entity ent = new Entity("lux_applicationendorsements");
                                        ent["lux_isdefault"] = true;
                                        ent["lux_endorsementhtml"] = Endorsement12.Attributes.Contains("lux_endorsementhtml") ? Endorsement12.Attributes["lux_endorsementhtml"] : "";
                                        ent["lux_endorsementnumber"] = Endorsement12.Attributes["new_endorsementnumber"];
                                        ent["lux_name"] = Endorsement12.Attributes["lux_name"];
                                        ent["lux_endorsementlibrary"] = new EntityReference("lux_endorsementlibrary", new Guid(Endorsement12.Attributes["lux_endorsementlibraryid"].ToString()));
                                        ent["lux_application"] = new EntityReference("lux_propertyownersapplications", appln.Id);
                                        service.Create(ent);
                                    }
                                }
                                else
                                {
                                    if (service.RetrieveMultiple(new FetchExpression(fetch1)).Entities.Count > 0)
                                    {
                                        service.Delete("lux_applicationendorsements", service.RetrieveMultiple(new FetchExpression(fetch1)).Entities[0].Id);
                                    }
                                }
                            }

                            var Endorsement13 = service.RetrieveMultiple(new FetchExpression(endorsementFetch)).Entities.FirstOrDefault(x => x.Attributes["lux_name"].ToString() == "Work Away Exclusion - Public and Products Liability");
                            if (Endorsement13 != null)
                            {
                                var fetch1 = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                                                          <entity name='lux_applicationendorsements'>
                                                            <attribute name='lux_applicationendorsementsid' />
                                                            <attribute name='lux_name' />
                                                            <attribute name='lux_endorsementnumber' />
                                                            <attribute name='createdon' />
                                                            <order attribute='lux_name' descending='false' />
                                                            <filter type='and'>
                                                              <condition attribute='statecode' operator='eq' value='0' />
                                                              <condition attribute='lux_endorsementlibrary' operator='eq' uiname='' uitype='lux_endorsementlibrary' value='{Endorsement13.Attributes["lux_endorsementlibraryid"]}' />
                                                              <condition attribute='lux_application' operator='eq' uiname='' uitype='lux_propertyownersapplications' value='{appln.Id}' />
                                                            </filter>
                                                          </entity>
                                                        </fetch>";

                                if (WorkAway == false)
                                {
                                    if (service.RetrieveMultiple(new FetchExpression(fetch1)).Entities.Count == 0)
                                    {
                                        Entity ent = new Entity("lux_applicationendorsements");
                                        ent["lux_isdefault"] = true;
                                        ent["lux_endorsementhtml"] = Endorsement13.Attributes.Contains("lux_endorsementhtml") ? Endorsement13.Attributes["lux_endorsementhtml"] : "";
                                        ent["lux_endorsementnumber"] = Endorsement13.Attributes["new_endorsementnumber"];
                                        ent["lux_name"] = Endorsement13.Attributes["lux_name"];
                                        ent["lux_endorsementlibrary"] = new EntityReference("lux_endorsementlibrary", new Guid(Endorsement13.Attributes["lux_endorsementlibraryid"].ToString()));
                                        ent["lux_application"] = new EntityReference("lux_propertyownersapplications", appln.Id);
                                        service.Create(ent);
                                    }
                                }
                                else
                                {
                                    if (service.RetrieveMultiple(new FetchExpression(fetch1)).Entities.Count > 0)
                                    {
                                        service.Delete("lux_applicationendorsements", service.RetrieveMultiple(new FetchExpression(fetch1)).Entities[0].Id);
                                    }
                                }
                            }

                            var Endorsement14 = service.RetrieveMultiple(new FetchExpression(endorsementFetch)).Entities.FirstOrDefault(x => x.Attributes["lux_name"].ToString() == "Alarm Requirement: Audible Signalling");
                            if (Endorsement14 != null)
                            {
                                var fetch1 = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                                                          <entity name='lux_applicationendorsements'>
                                                            <attribute name='lux_applicationendorsementsid' />
                                                            <attribute name='lux_name' />
                                                            <attribute name='lux_endorsementnumber' />
                                                            <attribute name='createdon' />
                                                            <order attribute='lux_name' descending='false' />
                                                            <filter type='and'>
                                                              <condition attribute='statecode' operator='eq' value='0' />
                                                              <condition attribute='lux_endorsementlibrary' operator='eq' uiname='' uitype='lux_endorsementlibrary' value='{Endorsement14.Attributes["lux_endorsementlibraryid"]}' />
                                                              <condition attribute='lux_application' operator='eq' uiname='' uitype='lux_propertyownersapplications' value='{appln.Id}' />
                                                            </filter>
                                                          </entity>
                                                        </fetch>";

                                if (IntruderAlarm == true && TypeOFAlarm == 972970001)//audible only
                                {
                                    if (service.RetrieveMultiple(new FetchExpression(fetch1)).Entities.Count == 0)
                                    {
                                        Entity ent = new Entity("lux_applicationendorsements");
                                        ent["lux_isdefault"] = true;
                                        ent["lux_endorsementhtml"] = Endorsement14.Attributes.Contains("lux_endorsementhtml") ? Endorsement14.Attributes["lux_endorsementhtml"] : "";
                                        ent["lux_endorsementnumber"] = Endorsement14.Attributes["new_endorsementnumber"];
                                        ent["lux_name"] = Endorsement14.Attributes["lux_name"];
                                        ent["lux_endorsementlibrary"] = new EntityReference("lux_endorsementlibrary", new Guid(Endorsement14.Attributes["lux_endorsementlibraryid"].ToString()));
                                        ent["lux_application"] = new EntityReference("lux_propertyownersapplications", appln.Id);
                                        service.Create(ent);
                                    }
                                }
                                else
                                {
                                    if (service.RetrieveMultiple(new FetchExpression(fetch1)).Entities.Count > 0)
                                    {
                                        service.Delete("lux_applicationendorsements", service.RetrieveMultiple(new FetchExpression(fetch1)).Entities[0].Id);
                                    }
                                }
                            }

                            var Endorsement15 = service.RetrieveMultiple(new FetchExpression(endorsementFetch)).Entities.FirstOrDefault(x => x.Attributes["lux_name"].ToString() == "Alarm Requirement: Non-Confirmed Signalling");
                            if (Endorsement15 != null)
                            {
                                var fetch1 = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                                                          <entity name='lux_applicationendorsements'>
                                                            <attribute name='lux_applicationendorsementsid' />
                                                            <attribute name='lux_name' />
                                                            <attribute name='lux_endorsementnumber' />
                                                            <attribute name='createdon' />
                                                            <order attribute='lux_name' descending='false' />
                                                            <filter type='and'>
                                                              <condition attribute='statecode' operator='eq' value='0' />
                                                              <condition attribute='lux_endorsementlibrary' operator='eq' uiname='' uitype='lux_endorsementlibrary' value='{Endorsement15.Attributes["lux_endorsementlibraryid"]}' />
                                                              <condition attribute='lux_application' operator='eq' uiname='' uitype='lux_propertyownersapplications' value='{appln.Id}' />
                                                            </filter>
                                                          </entity>
                                                        </fetch>";

                                if (IntruderAlarm == true && (TypeOFAlarm == 972970002 || TypeOFAlarm == 972970005))
                                {
                                    if (service.RetrieveMultiple(new FetchExpression(fetch1)).Entities.Count == 0)
                                    {
                                        Entity ent = new Entity("lux_applicationendorsements");
                                        ent["lux_isdefault"] = true;
                                        ent["lux_endorsementhtml"] = Endorsement15.Attributes.Contains("lux_endorsementhtml") ? Endorsement15.Attributes["lux_endorsementhtml"] : "";
                                        ent["lux_endorsementnumber"] = Endorsement15.Attributes["new_endorsementnumber"];
                                        ent["lux_name"] = Endorsement15.Attributes["lux_name"];
                                        ent["lux_endorsementlibrary"] = new EntityReference("lux_endorsementlibrary", new Guid(Endorsement15.Attributes["lux_endorsementlibraryid"].ToString()));
                                        ent["lux_application"] = new EntityReference("lux_propertyownersapplications", appln.Id);
                                        service.Create(ent);
                                    }
                                }
                                else
                                {
                                    if (service.RetrieveMultiple(new FetchExpression(fetch1)).Entities.Count > 0)
                                    {
                                        service.Delete("lux_applicationendorsements", service.RetrieveMultiple(new FetchExpression(fetch1)).Entities[0].Id);
                                    }
                                }
                            }

                            var Endorsement16 = service.RetrieveMultiple(new FetchExpression(endorsementFetch)).Entities.FirstOrDefault(x => x.Attributes["lux_name"].ToString() == "Alarm Requirement: Confirmed Signalling");
                            if (Endorsement16 != null)
                            {
                                var fetch1 = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                                                          <entity name='lux_applicationendorsements'>
                                                            <attribute name='lux_applicationendorsementsid' />
                                                            <attribute name='lux_name' />
                                                            <attribute name='lux_endorsementnumber' />
                                                            <attribute name='createdon' />
                                                            <order attribute='lux_name' descending='false' />
                                                            <filter type='and'>
                                                              <condition attribute='statecode' operator='eq' value='0' />
                                                              <condition attribute='lux_endorsementlibrary' operator='eq' uiname='' uitype='lux_endorsementlibrary' value='{Endorsement16.Attributes["lux_endorsementlibraryid"]}' />
                                                              <condition attribute='lux_application' operator='eq' uiname='' uitype='lux_propertyownersapplications' value='{appln.Id}' />
                                                            </filter>
                                                          </entity>
                                                        </fetch>";

                                if (IntruderAlarm == true && (TypeOFAlarm == 972970003 || TypeOFAlarm == 972970004))//redcare gsm or dualcom
                                {
                                    if (service.RetrieveMultiple(new FetchExpression(fetch1)).Entities.Count == 0)
                                    {
                                        Entity ent = new Entity("lux_applicationendorsements");
                                        ent["lux_isdefault"] = true;
                                        ent["lux_endorsementhtml"] = Endorsement16.Attributes.Contains("lux_endorsementhtml") ? Endorsement16.Attributes["lux_endorsementhtml"] : "";
                                        ent["lux_endorsementnumber"] = Endorsement16.Attributes["new_endorsementnumber"];
                                        ent["lux_name"] = Endorsement16.Attributes["lux_name"];
                                        ent["lux_endorsementlibrary"] = new EntityReference("lux_endorsementlibrary", new Guid(Endorsement16.Attributes["lux_endorsementlibraryid"].ToString()));
                                        ent["lux_application"] = new EntityReference("lux_propertyownersapplications", appln.Id);
                                        service.Create(ent);
                                    }
                                }
                                else
                                {
                                    if (service.RetrieveMultiple(new FetchExpression(fetch1)).Entities.Count > 0)
                                    {
                                        service.Delete("lux_applicationendorsements", service.RetrieveMultiple(new FetchExpression(fetch1)).Entities[0].Id);
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }
    }
}
