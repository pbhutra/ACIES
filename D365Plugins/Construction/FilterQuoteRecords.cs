using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;

namespace D365Plugins
{
    public class FilterQuoteRecords : IPlugin
    {
        public void Execute(IServiceProvider serviceProvider)
        {
            IPluginExecutionContext context = (IPluginExecutionContext)serviceProvider.GetService(typeof(IPluginExecutionContext));
            ITracingService tracingService = (ITracingService)serviceProvider.GetService(typeof(ITracingService));

            IOrganizationServiceFactory serviceFactory = (IOrganizationServiceFactory)serviceProvider.GetService(typeof(IOrganizationServiceFactory));
            IOrganizationService organizationService = serviceFactory.CreateOrganizationService(context.UserId);

            Guid userID = context.InitiatingUserId;
            tracingService.Trace(userID.ToString());
            tracingService.Trace(context.Mode.ToString());
            tracingService.Trace(context.Stage.ToString());
            if (context.MessageName.Equals("RetrieveMultiple"))
            {
                if (context.InputParameters.Contains("Query"))
                {
                    if (context.InputParameters["Query"] is FetchExpression) // Normal View
                    {
                        // Get the QueryExpression from the property bag
                        FetchExpression objFetchExpression = (FetchExpression)context.InputParameters["Query"];

                        XDocument fetchXmlDoc = XDocument.Parse(objFetchExpression.Query);

                        //The required entity element
                        var entityElement = fetchXmlDoc.Descendants("entity").FirstOrDefault();
                        var entityName = entityElement.Attributes("name").FirstOrDefault().Value;

                        if (entityName == "lux_propertyownersapplications")
                        {
                            //Get all filter elements
                            var filterElements = entityElement.Descendants("filter");

                            //Find any existing relationshiptype conditions
                            var relationshiptypeConditions = from c in filterElements.Descendants("condition")
                                                             where c.Attribute("attribute").Value.Equals("lux_insuranceproductrequired")
                                                             select c;
                            if (relationshiptypeConditions.Count() > 0)
                            {
                                tracingService.Trace("Removing existing statecode filter conditions.");
                                //Remove relationshiptype conditions
                                relationshiptypeConditions.ToList().ForEach(x => x.Remove());
                            }

                            if (UserHasRole(userID, "System Administrator", organizationService, tracingService) == true)
                            {
                                //Add the condition you want in a new filter
                                entityElement.Add(
                                    new XElement("filter",
                                        new XElement("condition",
                                            new XAttribute("attribute", "lux_insuranceproductrequired"),
                                            new XAttribute("operator", "eq"), //equal
                                            new XAttribute("value", "5cae3bd2-1f78-eb11-a812-00224841494b") //comp
                                            )
                                        )
                                    );
                                objFetchExpression.Query = fetchXmlDoc.ToString();
                                tracingService.Trace("Yes");
                                tracingService.Trace(fetchXmlDoc.ToString());
                            }
                            else
                            {
                                //Add the condition you want in a new filter
                                entityElement.Add(
                                    new XElement("filter",
                                        new XElement("condition",
                                            new XAttribute("attribute", "lux_insuranceproductrequired"),
                                            new XAttribute("operator", "ne"), //equal
                                            new XAttribute("value", "5cae3bd2-1f78-eb11-a812-00224841494b") //comp
                                            )
                                        )
                                    );
                                objFetchExpression.Query = fetchXmlDoc.ToString();
                            }
                        }
                    }
                    else if (context.InputParameters["Query"] is QueryExpression) // Advance Find
                    {
                        QueryExpression objQueryExpression = (QueryExpression)context.InputParameters["Query"];
                        if (objQueryExpression.EntityName.Equals("lux_propertyownersapplications"))
                        {
                            tracingService.Trace("Query on Account confirmed");

                            //Recursively remove any conditions referring to the relationship type column
                            foreach (FilterExpression fe in objQueryExpression.Criteria.Filters)
                            {
                                //Remove any existing criteria based on relationship column
                                RemoveAttributeConditions(fe, "lux_insuranceproductrequired", tracingService);
                            }

                            //Define the filter
                            var relationshipCodeFilter = new FilterExpression();



                            if (UserHasRole(userID, "System Administrator", organizationService, tracingService) == true)
                            {
                                relationshipCodeFilter.AddCondition("lux_insuranceproductrequired", ConditionOperator.Equal, "5cae3bd2-1f78-eb11-a812-00224841494b");
                                //Add it to the Criteria
                                objQueryExpression.Criteria.AddFilter(relationshipCodeFilter);
                            }
                            else
                            {
                                relationshipCodeFilter.AddCondition("lux_insuranceproductrequired", ConditionOperator.NotEqual, "5cae3bd2-1f78-eb11-a812-00224841494b");
                                //Add it to the Criteria
                                objQueryExpression.Criteria.AddFilter(relationshipCodeFilter);
                            }
                        }
                    }
                }
            }
        }

        private void RemoveAttributeConditions(FilterExpression filter, string attributeName, ITracingService tracingService)
        {
            List<ConditionExpression> conditionsToRemove = new List<ConditionExpression>();

            foreach (ConditionExpression ce in filter.Conditions)
            {
                if (ce.AttributeName.Equals(attributeName))
                {
                    conditionsToRemove.Add(ce);
                }
            }

            conditionsToRemove.ForEach(x =>
            {
                filter.Conditions.Remove(x);
                tracingService.Trace("Removed existing relationshipetype filter conditions.");
            });

            foreach (FilterExpression fe in filter.Filters)
            {
                RemoveAttributeConditions(fe, attributeName, tracingService);
            }
        }

        private static bool UserHasRole(Guid userID, string RoleName, IOrganizationService service, ITracingService tracingService)
        {
            bool hasRole = false;
            QueryExpression qe = new QueryExpression("systemuserroles");
            qe.Criteria.AddCondition("systemuserid", ConditionOperator.Equal, userID);
            LinkEntity link = qe.AddLink("role", "roleid", "roleid", JoinOperator.Inner);
            link.LinkCriteria.AddCondition("name", ConditionOperator.Equal, RoleName);
            EntityCollection results = service.RetrieveMultiple(qe);
            hasRole = results.Entities.Count > 0;
            tracingService.Trace(hasRole.ToString());
            return hasRole;
        }
    }
}