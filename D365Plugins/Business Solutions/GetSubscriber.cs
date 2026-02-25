using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace D365Plugins
{
    public class GetSubscriber : IPlugin
    {
        public void Execute(IServiceProvider serviceProvider)
        {
            try
            {
                IPluginExecutionContext context = (IPluginExecutionContext)serviceProvider.GetService(typeof(IPluginExecutionContext));
                IOrganizationServiceFactory serviceFactory = (IOrganizationServiceFactory)serviceProvider.GetService(typeof(IOrganizationServiceFactory));
                IOrganizationService organizationService = serviceFactory.CreateOrganizationService(context.UserId);

                var ContactId = context.InputParameters.Contains("ContactId") ? context.InputParameters["ContactId"].ToString() : "";
                if (ContactId != "")
                {
                    EntityCollection collection = new EntityCollection();
                    var contactFetch = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                                              <entity name='contact'>
                                                <attribute name='fullname' />
                                                <attribute name='parentcustomerid' />
                                                <attribute name='telephone1' />
                                                <attribute name='emailaddress1' />
                                                <attribute name='middlename' />
                                                <attribute name='lastname' />
                                                <attribute name='firstname' />
                                                <attribute name='jobtitle' />
                                                <attribute name='address1_city' />
                                                <attribute name='lux_country' />
                                                <attribute name='address1_stateorprovince' />
                                                <attribute name='address1_postalcode' />
                                                <attribute name='mobilephone' />
                                                <attribute name='address1_line3' />
                                                <attribute name='address1_line2' />
                                                <attribute name='address1_line1' />
                                                <attribute name='address1_fax' />
                                                <attribute name='lux_imcanumber' />
                                                <attribute name='lux_cfanumber' />
                                                <attribute name='lux_ria' />
                                                <attribute name='lux_jobfunction' />
                                                <attribute name='contactid' />
                                                <order attribute='fullname' descending='false' />
                                                <filter type='and'>
                                                  <condition attribute='statecode' operator='eq' value='0' />
                                                  <condition attribute='contactid' operator='eq' uiname='' uitype='contact' value='{ContactId}' />
                                                </filter>
                                              </entity>
                                            </fetch>";

                    var contactList = organizationService.RetrieveMultiple(new FetchExpression(contactFetch));
                    if (contactList.Entities.Count() > 0)
                    {
                        foreach (var item in contactList.Entities)
                        {
                            var contact = contactList.Entities.FirstOrDefault();
                            var FirstName = contact.Attributes.Contains("firstname") ? contact.Attributes["firstname"].ToString() : "";
                            var LastName = contact.Attributes.Contains("lastname") ? contact.Attributes["lastname"].ToString() : "";
                            var EmailAddress1 = contact.Attributes.Contains("emailaddress1") ? contact.Attributes["emailaddress1"].ToString() : "";
                            var MobilePhone = contact.Attributes.Contains("mobilephone") ? contact.Attributes["mobilephone"].ToString() : "";
                            var Fax = contact.Attributes.Contains("address1_fax") ? contact.Attributes["address1_fax"].ToString() : "";
                            var JobTitle = contact.Attributes.Contains("jobtitle") ? contact.Attributes["jobtitle"].ToString() : "";
                            var Address1Line1 = contact.Attributes.Contains("address1_line1") ? contact.Attributes["address1_line1"].ToString() : "";
                            var Address1Line2 = contact.Attributes.Contains("address1_line2") ? contact.Attributes["address1_line2"].ToString() : "";
                            var Address1Line3 = contact.Attributes.Contains("address1_line3") ? contact.Attributes["address1_line3"].ToString() : "";
                            var Address1City = contact.Attributes.Contains("address1_city") ? contact.Attributes["address1_city"].ToString() : "";
                            var Address1PostalCode = contact.Attributes.Contains("address1_postalcode") ? contact.Attributes["address1_postalcode"].ToString() : "";
                            var Address1County = contact.Attributes.Contains("address1_stateorprovince") ? contact.Attributes["address1_stateorprovince"].ToString() : "";
                            var Address1Country = contact.Attributes.Contains("lux_country") ? contact.FormattedValues["lux_country"].ToString() : "";
                            var IMCANumber = contact.Attributes.Contains("lux_imcanumber") ? contact.Attributes["lux_imcanumber"].ToString() : "";
                            var CFANumber = contact.Attributes.Contains("lux_cfanumber") ? contact.Attributes["lux_cfanumber"].ToString() : "";
                            var RIA = contact.Attributes.Contains("lux_ria") ? contact.FormattedValues["lux_ria"].ToString() : "";
                            var JobFunction = contact.Attributes.Contains("lux_jobfunction") ? contact.Attributes["lux_jobfunction"].ToString() : "";
                            var Country = contact.Attributes.Contains("lux_country") ? contact.FormattedValues["lux_country"].ToString() : "";

                            Entity ent = new Entity();
                            ent.Attributes["FirstName"] = FirstName;
                            ent.Attributes["LastName"] = LastName;
                            ent.Attributes["EmailAddress1"] = EmailAddress1;
                            ent.Attributes["MobilePhone"] = MobilePhone;
                            ent.Attributes["Fax"] = Fax;
                            ent.Attributes["JobTitle"] = JobTitle;
                            ent.Attributes["Address1Line1"] = Address1Line1;
                            ent.Attributes["Address1Line2"] = Address1Line2;
                            ent.Attributes["Address1Line3"] = Address1Line3;
                            ent.Attributes["Address1City"] = Address1City;
                            ent.Attributes["Address1PostalCode"] = Address1PostalCode;
                            ent.Attributes["Address1County"] = Address1County;
                            ent.Attributes["Address1Country"] = Address1Country;
                            ent.Attributes["IMCANumber"] = IMCANumber;
                            ent.Attributes["CFANumber"] = CFANumber;
                            ent.Attributes["RIA"] = RIA;
                            ent.Attributes["JobFunction"] = JobFunction;
                            ent.Attributes["Country"] = Country;

                            collection.Entities.Add(ent);
                        }
                        context.OutputParameters["value"] = collection;
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
