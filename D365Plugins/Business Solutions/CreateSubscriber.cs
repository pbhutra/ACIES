using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace D365Plugins
{
    public class CreateSubscriber : IPlugin
    {
        public void Execute(IServiceProvider serviceProvider)
        {
            try
            {
                IPluginExecutionContext context = (IPluginExecutionContext)serviceProvider.GetService(typeof(IPluginExecutionContext));
                IOrganizationServiceFactory serviceFactory = (IOrganizationServiceFactory)serviceProvider.GetService(typeof(IOrganizationServiceFactory));
                IOrganizationService organizationService = serviceFactory.CreateOrganizationService(context.UserId);

                var firstname = context.InputParameters.Contains("FirstName") ? context.InputParameters["FirstName"].ToString() : "";
                var lastname = context.InputParameters.Contains("LastName") ? context.InputParameters["LastName"].ToString() : "";
                var emailaddress = context.InputParameters.Contains("EmailAddress") ? context.InputParameters["EmailAddress"].ToString() : "";
                var countrycode = context.InputParameters.Contains("CountryCode") ? context.InputParameters["CountryCode"].ToString() : "";
                var jobtitle = context.InputParameters.Contains("JobTitle") ? context.InputParameters["JobTitle"].ToString() : "";
                var CompanyName = context.InputParameters.Contains("CompanyName") ? context.InputParameters["CompanyName"].ToString() : "";
                var Telephone = context.InputParameters.Contains("Telephone") ? context.InputParameters["Telephone"] : "";
                DateTime LastUpdatedDate = context.InputParameters.Contains("LastUpdatedDate") ? Convert.ToDateTime(context.InputParameters["LastUpdatedDate"]) : DateTime.UtcNow;

                Entity contact = new Entity("contact");
                contact["firstname"] = firstname;
                contact["lastname"] = lastname;
                contact["emailaddress1"] = emailaddress;
                contact["jobtitle"] = jobtitle;
                if (Telephone != null)
                    contact["mobilephone"] = Telephone.ToString();

                var accountFetch = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                                          <entity name='account'>
                                            <attribute name='name' />
                                            <attribute name='telephone1' />
                                            <attribute name='address1_city' />
                                            <attribute name='primarycontactid' />
                                            <attribute name='statecode' />
                                            <filter type='and'>
                                              <condition attribute='name' operator='eq' value='{CompanyName}' />
                                            </filter>
                                          </entity>
                                        </fetch>";

                var accountList = organizationService.RetrieveMultiple(new FetchExpression(accountFetch));
                if (accountList.Entities.Count() > 0)
                {
                    contact["parentcustomerid"] = new EntityReference("account", accountList.Entities.FirstOrDefault().Id);
                }
                else
                {
                    Entity account = new Entity("account");
                    account["name"] = CompanyName;
                    var accountID = organizationService.Create(account);

                    contact["parentcustomerid"] = new EntityReference("account", accountID);
                }

                var countryFetch = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                                          <entity name='lux_country'>
                                            <attribute name='lux_countryid' />
                                            <attribute name='lux_name' />
                                            <attribute name='lux_countrycode' />
                                            <attribute name='createdon' />
                                            <order attribute='lux_name' descending='false' />
                                            <filter type='and'>
                                              <condition attribute='statecode' operator='eq' value='0' />
                                              <condition attribute='lux_countrycode' operator='eq' value='{countrycode}' />
                                            </filter>
                                          </entity>
                                        </fetch>";

                var countryList = organizationService.RetrieveMultiple(new FetchExpression(countryFetch));
                if (countryList.Entities.Count() > 0)
                {
                    contact["lux_country"] = new EntityReference("lux_country", countryList.Entities.FirstOrDefault().Id);
                }

                var ContactID = organizationService.Create(contact);

                Entity ent = new Entity();
                ent["ContactID"] = ContactID;
                context.OutputParameters["ContactID"] = ent;
            }
            catch (Exception ex)
            {
                throw ex.InnerException;
            }
        }
    }
}
