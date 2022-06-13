using Microsoft.Crm.Sdk.Messages;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;

namespace Microsoft.PowerPlatform.Dataverse.Client.Extensions
{
    /// <summary>
    /// Extensions to support more generic record interaction mechanic's 
    /// </summary>
    public static class CRUDExtentions
    {
        /// <summary>
        /// Uses the dynamic entity patter to create a new entity
        /// </summary>
        /// <param name="entityName">Name of Entity To create</param>
        /// <param name="valueArray">Initial Values</param>
        /// <param name="applyToSolution">Optional: Applies the update with a solution by Unique name</param>
        /// <param name="enabledDuplicateDetection">Optional: if true, enabled Dataverse onboard duplicate detection</param>
        /// <param name="batchId">Optional: if set to a valid GUID, generated by the Create Batch Request Method, will assigned the request to the batch for later execution, on fail, runs the request immediately </param>
        /// <param name="bypassPluginExecution">Adds the bypass plugin behavior to this request. Note: this will only apply if the caller has the prvBypassPlugins permission to bypass plugins.  If its attempted without the permission the request will fault.</param>
        /// <param name="serviceClient">ServiceClient</param>
        /// <returns>Guid on Success, Guid.Empty on fail</returns>
        public static Guid CreateNewRecord(this ServiceClient serviceClient, string entityName, Dictionary<string, DataverseDataTypeWrapper> valueArray, string applyToSolution = "", bool enabledDuplicateDetection = false, Guid batchId = default(Guid), bool bypassPluginExecution = false)
        {
            serviceClient._logEntry.ResetLastError();  // Reset Last Error

            if (serviceClient.DataverseService == null)
            {
                serviceClient._logEntry.Log("Dataverse Service not initialized", TraceEventType.Error);
                return Guid.Empty;
            }

            if (string.IsNullOrEmpty(entityName))
                return Guid.Empty;

            if ((valueArray == null) || (valueArray.Count == 0))
                return Guid.Empty;


            // Create the New Entity Type.
            Entity NewEnt = new Entity();
            NewEnt.LogicalName = entityName;

            AttributeCollection propList = new AttributeCollection();
            foreach (KeyValuePair<string, DataverseDataTypeWrapper> i in valueArray)
            {
                serviceClient.AddValueToPropertyList(i, propList);
            }

            NewEnt.Attributes.AddRange(propList);

            CreateRequest createReq = new CreateRequest();
            createReq.Target = NewEnt;
            createReq.Parameters.Add("SuppressDuplicateDetection", !enabledDuplicateDetection);
            if (!string.IsNullOrWhiteSpace(applyToSolution))
                createReq.Parameters[Utilities.RequestHeaders.SOLUTIONUNIQUENAME] = applyToSolution;

            CreateResponse createResp = null;

            if (serviceClient.AddRequestToBatch(batchId, createReq, entityName, string.Format(CultureInfo.InvariantCulture, "Request for Create on {0} queued", entityName), bypassPluginExecution))
                return Guid.Empty;

            createResp = (CreateResponse)serviceClient.ExecuteOrganizationRequestImpl(createReq, entityName, useWebAPI: true, bypassPluginExecution: bypassPluginExecution);
            if (createResp != null)
            {
                return createResp.id;
            }
            else
                return Guid.Empty;

        }

        /// <summary>
        /// Generic update entity
        /// </summary>
        /// <param name="entityName">String version of the entity name</param>
        /// <param name="keyFieldName">Key fieldname of the entity </param>
        /// <param name="id">Guid ID of the entity to update</param>
        /// <param name="fieldList">Fields to update</param>
        /// <param name="applyToSolution">Optional: Applies the update with a solution by Unique name</param>
        /// <param name="enabledDuplicateDetection">Optional: if true, enabled Dataverse onboard duplicate detection</param>
        /// <param name="batchId">Optional: if set to a valid GUID, generated by the Create Batch Request Method, will assigned the request to the batch for later execution, on fail, runs the request immediately </param>
		/// <param name="bypassPluginExecution">Adds the bypass plugin behavior to this request. Note: this will only apply if the caller has the prvBypassPlugins permission to bypass plugins.  If its attempted without the permission the request will fault.</param>
        /// <param name="serviceClient">ServiceClient</param>
        /// <returns>true on success, false on fail</returns>
        public static bool UpdateEntity(this ServiceClient serviceClient, string entityName, string keyFieldName, Guid id, Dictionary<string, DataverseDataTypeWrapper> fieldList, string applyToSolution = "", bool enabledDuplicateDetection = false, Guid batchId = default(Guid), bool bypassPluginExecution = false)
        {
            serviceClient._logEntry.ResetLastError();  // Reset Last Error
            if (serviceClient.DataverseService == null || id == Guid.Empty)
            {
                return false;
            }

            if (fieldList == null || fieldList.Count == 0)
                return false;

            Entity uEnt = new Entity();
            uEnt.LogicalName = entityName;


            AttributeCollection PropertyList = new AttributeCollection();

            #region MapCode
            foreach (KeyValuePair<string, DataverseDataTypeWrapper> field in fieldList)
            {
                serviceClient.AddValueToPropertyList(field, PropertyList);
            }

            // Add the key...
            // check to see if the key is in the import set already
            if (!fieldList.ContainsKey(keyFieldName))
                PropertyList.Add(new KeyValuePair<string, object>(keyFieldName, id));

            #endregion

            uEnt.Attributes.AddRange(PropertyList.ToArray());
            uEnt.Id = id;

            UpdateRequest req = new UpdateRequest();
            req.Target = uEnt;

            req.Parameters.Add("SuppressDuplicateDetection", !enabledDuplicateDetection);
            if (!string.IsNullOrWhiteSpace(applyToSolution))
                req.Parameters.Add("SolutionUniqueName", applyToSolution);


            if (serviceClient.AddRequestToBatch(batchId, req, string.Format(CultureInfo.InvariantCulture, "Updating {0} : {1}", entityName, id.ToString()), string.Format(CultureInfo.InvariantCulture, "Request for update on {0} queued", entityName), bypassPluginExecution))
                return false;

            UpdateResponse resp = (UpdateResponse)serviceClient.ExecuteOrganizationRequestImpl(req, string.Format(CultureInfo.InvariantCulture, "Updating {0} : {1}", entityName, id.ToString()), useWebAPI: true, bypassPluginExecution: bypassPluginExecution);
            if (resp == null)
                return false;
            else
                return true;
        }


        /// <summary>
        /// Updates the State and Status of the Entity passed in.
        /// </summary>
        /// <param name="entName">Name of the entity</param>
        /// <param name="id">Guid ID of the entity you are updating</param>
        /// <param name="stateCode">String version of the new state</param>
        /// <param name="statusCode">String Version of the new status</param>
        /// <param name="batchId">Optional : Batch ID  to attach this request too.</param>
		/// <param name="bypassPluginExecution">Adds the bypass plugin behavior to this request. Note: this will only apply if the caller has the prvBypassPlugins permission to bypass plugins.  If its attempted without the permission the request will fault.</param>
        /// <param name="serviceClient">ServiceClient</param>
        /// <returns>true on success. </returns>
        public static bool UpdateStateAndStatusForEntity(this ServiceClient serviceClient, string entName, Guid id, string stateCode, string statusCode, Guid batchId = default(Guid), bool bypassPluginExecution = false)
        {
            return serviceClient.UpdateStateStatusForEntity(entName, id, stateCode, statusCode, batchId: batchId, bypassPluginExecution: bypassPluginExecution);
        }

        /// <summary>
        /// Updates the State and Status of the Entity passed in.
        /// </summary>
        /// <param name="entName">Name of the entity</param>
        /// <param name="id">Guid ID of the entity you are updating</param>
        /// <param name="stateCode">Int version of the new state</param>
        /// <param name="statusCode">Int Version of the new status</param>
        /// <param name="batchId">Optional : Batch ID  to attach this request too.</param>
        /// <param name="bypassPluginExecution">Adds the bypass plugin behavior to this request. Note: this will only apply if the caller has the prvBypassPlugins permission to bypass plugins.  If its attempted without the permission the request will fault.</param>
        /// <param name="serviceClient">ServiceClient</param>
        /// <returns>true on success. </returns>
        public static bool UpdateStateAndStatusForEntity(this ServiceClient serviceClient, string entName, Guid id, int stateCode, int statusCode, Guid batchId = default(Guid), bool bypassPluginExecution = false)
        {
            return serviceClient.UpdateStateStatusForEntity(entName, id, string.Empty, string.Empty, stateCode, statusCode, batchId, bypassPluginExecution);
        }

        /// <summary>
        /// Deletes an entity from the Dataverse
        /// </summary>
        /// <param name="entityType">entity type name</param>
        /// <param name="entityId">entity id</param>
        /// <param name="batchId">Optional : Batch ID  to attach this request too.</param>
        /// <param name="bypassPluginExecution">Adds the bypass plugin behavior to this request. Note: this will only apply if the caller has the prvBypassPlugins permission to bypass plugins.  If its attempted without the permission the request will fault.</param>
        /// <param name="serviceClient">ServiceClient</param>
        /// <returns>true on success, false on failure</returns>
        public static bool DeleteEntity(this ServiceClient serviceClient, string entityType, Guid entityId, Guid batchId = default(Guid), bool bypassPluginExecution = false)
        {
            serviceClient._logEntry.ResetLastError();  // Reset Last Error
            if (serviceClient.DataverseService == null)
            {
                serviceClient._logEntry.Log("Dataverse Service not initialized", TraceEventType.Error);
                return false;
            }

            DeleteRequest req = new DeleteRequest();
            req.Target = new EntityReference(entityType, entityId);

            if (batchId != Guid.Empty)
            {
                if (serviceClient.IsBatchOperationsAvailable)
                {
                    if (serviceClient._batchManager.AddNewRequestToBatch(batchId, req, string.Format(CultureInfo.InvariantCulture, "Trying to Delete. Entity = {0}, ID = {1}", entityType, entityId)))
                    {
                        serviceClient._logEntry.Log(string.Format(CultureInfo.InvariantCulture, "Request for Delete on {0} queued", entityType), TraceEventType.Verbose);
                        return false;
                    }
                    else
                        serviceClient._logEntry.Log("Unable to add request to batch queue, Executing normally", TraceEventType.Warning);
                }
                else
                {
                    // Error and fall though.
                    serviceClient._logEntry.Log("Unable to add request to batch, Batching is not currently available, Executing normally", TraceEventType.Warning);
                }
            }

            if (batchId != Guid.Empty)
            {
                if (serviceClient.IsBatchOperationsAvailable)
                {
                    if (serviceClient._batchManager.AddNewRequestToBatch(batchId, req, string.Format(CultureInfo.InvariantCulture, "Delete Entity = {0}, ID = {1}  queued", entityType, entityId)))
                    {
                        serviceClient._logEntry.Log(string.Format(CultureInfo.InvariantCulture, "Request for Delete. Entity = {0}, ID = {1}  queued", entityType, entityId), TraceEventType.Verbose);
                        return false;
                    }
                    else
                        serviceClient._logEntry.Log("Unable to add request to batch queue, Executing normally", TraceEventType.Warning);
                }
                else
                {
                    // Error and fall though.
                    serviceClient._logEntry.Log("Unable to add request to batch, Batching is not currently available, Executing normally", TraceEventType.Warning);
                }
            }

            if (serviceClient.AddRequestToBatch(batchId, req, string.Format(CultureInfo.InvariantCulture, "Trying to Delete. Entity = {0}, ID = {1}", entityType, entityId), string.Format(CultureInfo.InvariantCulture, "Request to Delete. Entity = {0}, ID = {1} Queued", entityType, entityId), bypassPluginExecution))
                return false;

            DeleteResponse resp = (DeleteResponse)serviceClient.ExecuteOrganizationRequestImpl(req, string.Format(CultureInfo.InvariantCulture, "Trying to Delete. Entity = {0}, ID = {1}", entityType, entityId), useWebAPI: true, bypassPluginExecution: bypassPluginExecution);
            if (resp != null)
            {
                // Clean out the cache if the account happens to be stored in there.
                if ((serviceClient._CachObject != null) && (serviceClient._CachObject.ContainsKey(entityType)))
                {
                    while (serviceClient._CachObject[entityType].ContainsValue(entityId))
                    {
                        foreach (KeyValuePair<string, Guid> v in serviceClient._CachObject[entityType].Values)
                        {
                            if (v.Value == entityId)
                            {
                                serviceClient._CachObject[entityType].Remove(v.Key);
                                break;
                            }
                        }
                    }
                }
                return true;
            }
            return false;
        }

        /// <summary>
        /// This creates a annotation [note] entry, related to a an existing entity
        /// <para>Required Properties in the fieldList</para>
        /// <para>notetext (string) = Text of the note,  </para>
        /// <para>subject (string) = this is the title of the note</para>
        /// </summary>
        /// <param name="targetEntityTypeName">Target Entity TypeID</param>
        /// <param name="targetEntityId">Target Entity ID</param>
        /// <param name="fieldList">Fields to populate</param>
        /// <param name="batchId">Optional: if set to a valid GUID, generated by the Create Batch Request Method, will assigned the request to the batch for later execution, on fail, runs the request immediately </param>
        /// <param name="bypassPluginExecution">Adds the bypass plugin behavior to this request. Note: this will only apply if the caller has the prvBypassPlugins permission to bypass plugins.  If its attempted without the permission the request will fault.</param>
        /// <param name="serviceClient">ServiceClient</param>
        /// <returns></returns>
        public static Guid CreateAnnotation(this ServiceClient serviceClient, string targetEntityTypeName, Guid targetEntityId, Dictionary<string, DataverseDataTypeWrapper> fieldList, Guid batchId = default(Guid), bool bypassPluginExecution = false)
        {
            serviceClient._logEntry.ResetLastError();  // Reset Last Error

            if (string.IsNullOrEmpty(targetEntityTypeName))
                return Guid.Empty;

            if (targetEntityId == Guid.Empty)
                return Guid.Empty;

            if (fieldList == null)
                fieldList = new Dictionary<string, DataverseDataTypeWrapper>();

            fieldList.Add("objecttypecode", new DataverseDataTypeWrapper(targetEntityTypeName, DataverseFieldType.String));
            fieldList.Add("objectid", new DataverseDataTypeWrapper(targetEntityId, DataverseFieldType.Lookup, targetEntityTypeName));
            fieldList.Add("ownerid", new DataverseDataTypeWrapper(serviceClient.SystemUser.UserId, DataverseFieldType.Lookup, "systemuser"));

            return serviceClient.CreateNewRecord("annotation", fieldList, batchId: batchId, bypassPluginExecution: bypassPluginExecution);

        }

        /// <summary>
        /// Creates a new activity against the target entity type
        /// </summary>
        /// <param name="activityEntityTypeName">Type of Activity you would like to create</param>
        /// <param name="regardingEntityTypeName">Entity type of the Entity you want to associate with.</param>
        /// <param name="subject">Subject Line of the Activity</param>
        /// <param name="description">Description Text of the Activity </param>
        /// <param name="regardingId">ID of the Entity to associate the Activity too</param>
        /// <param name="creatingUserId">User ID that Created the Activity *Calling user must have necessary permissions to assign to another user</param>
        /// <param name="fieldList">Additional fields to add as part of the activity creation</param>
        /// <param name="batchId">Optional: if set to a valid GUID, generated by the Create Batch Request Method, will assigned the request to the batch for later execution, on fail, runs the request immediately </param>
		/// <param name="bypassPluginExecution">Adds the bypass plugin behavior to this request. Note: this will only apply if the caller has the prvBypassPlugins permission to bypass plugins.  If its attempted without the permission the request will fault.</param>
        /// <param name="serviceClient">ServiceClient</param>
        /// <returns>Guid of Activity ID or Guid.empty</returns>
        public static Guid CreateNewActivityEntry(this ServiceClient serviceClient,
            string activityEntityTypeName,
            string regardingEntityTypeName,
            Guid regardingId,
            string subject,
            string description,
            string creatingUserId,
            Dictionary<string, DataverseDataTypeWrapper> fieldList = null,
            Guid batchId = default(Guid),
            bool bypassPluginExecution = false
            )
        {

            #region PreChecks
            serviceClient._logEntry.ResetLastError();  // Reset Last Error
            if (serviceClient.DataverseService == null)
            {
                serviceClient._logEntry.Log("Dataverse Service not initialized", TraceEventType.Error);
                return Guid.Empty;
            }
            if (string.IsNullOrWhiteSpace(activityEntityTypeName))
            {
                serviceClient._logEntry.Log("You must specify the activity type name to create", TraceEventType.Error);
                return Guid.Empty;
            }
            if (string.IsNullOrWhiteSpace(subject))
            {
                serviceClient._logEntry.Log(string.Format(CultureInfo.InvariantCulture, "A Subject is required to create an activity of type {0}", regardingEntityTypeName), TraceEventType.Error);
                return Guid.Empty;
            }
            #endregion

            Guid activityId = Guid.Empty;
            try
            {
                // reuse the passed in field list if its available, else punt and create a new one.
                if (fieldList == null)
                    fieldList = new Dictionary<string, DataverseDataTypeWrapper>();

                fieldList.Add("subject", new DataverseDataTypeWrapper(subject, DataverseFieldType.String));
                if (regardingId != Guid.Empty)
                    fieldList.Add("regardingobjectid", new DataverseDataTypeWrapper(regardingId, DataverseFieldType.Lookup, regardingEntityTypeName));
                if (!string.IsNullOrWhiteSpace(description))
                    fieldList.Add("description", new DataverseDataTypeWrapper(description, DataverseFieldType.String));

                // Create the base record.
                activityId = serviceClient.CreateNewRecord(activityEntityTypeName, fieldList, bypassPluginExecution: bypassPluginExecution);

                // if I have a user ID,  try to assign it to that user.
                if (!string.IsNullOrWhiteSpace(creatingUserId))
                {
                    Guid userId = serviceClient.GetLookupValueForEntity("systemuser", creatingUserId);

                    if (userId != Guid.Empty)
                    {
                        EntityReference newAction = new EntityReference(activityEntityTypeName, activityId);
                        EntityReference principal = new EntityReference("systemuser", userId);

                        AssignRequest arRequest = new AssignRequest();
                        arRequest.Assignee = principal;
                        arRequest.Target = newAction;
                        if (serviceClient.AddRequestToBatch(batchId, arRequest, string.Format(CultureInfo.InvariantCulture, "Trying to Assign a Record. Entity = {0} , ID = {1}", newAction.LogicalName, principal.LogicalName),
                                                string.Format(CultureInfo.InvariantCulture, "Request to Assign a Record. Entity = {0} , ID = {1} Queued", newAction.LogicalName, principal.LogicalName), bypassPluginExecution))
                            return Guid.Empty;
                        serviceClient.Command_Execute(arRequest, "Assign Activity", bypassPluginExecution);
                    }
                }
            }
            catch (Exception exp)
            {
                serviceClient._logEntry.Log(exp);
            }
            return activityId;
        }

        /// <summary>
        /// Closes the Activity type specified.
        /// The Activity Entity type supports fax , letter , and phonecall
        /// <para>*Note: This will default to using English names for Status. if you need to use Non-English, you should populate the names for completed for the status and state.</para>
        /// </summary>
        /// <param name="activityEntityType">Type of Activity you would like to close.. Supports fax, letter, phonecall</param>
        /// <param name="activityId">ID of the Activity you want to close</param>
        /// <param name="stateCode">State Code configured on the activity</param>
        /// <param name="statusCode">Status code on the activity </param>
        /// <param name="batchId">Optional: if set to a valid GUID, generated by the Create Batch Request Method, will assigned the request to the batch for later execution, on fail, runs the request immediately </param>
        /// <param name="bypassPluginExecution">Adds the bypass plugin behavior to this request. Note: this will only apply if the caller has the prvBypassPlugins permission to bypass plugins.  If its attempted without the permission the request will fault.</param>
        /// <param name="serviceClient">ServiceClient</param>
        /// <returns>true if success false if not.</returns>
        public static bool CloseActivity(this ServiceClient serviceClient,
            string activityEntityType,
            Guid activityId,
            string stateCode = "completed",
            string statusCode = "completed",
            Guid batchId = default(Guid),
            bool bypassPluginExecution = false
            )
        {
            return serviceClient.UpdateStateStatusForEntity(activityEntityType, activityId, stateCode, statusCode, batchId: batchId, bypassPluginExecution: bypassPluginExecution);
        }

        /// <summary>
        /// Updates the state of an activity
        /// </summary>
        /// <param name="entName"></param>
        /// <param name="entId"></param>
        /// <param name="newState"></param>
        /// <param name="newStatus"></param>
        /// <param name="newStateid">ID for the new State ( Skips metadata lookup )</param>
        /// <param name="newStatusid">ID for new Status ( Skips Metadata Lookup)</param>
        /// <param name="batchId">Optional: if set to a valid GUID, generated by the Create Batch Request Method, will assigned the request to the batch for later execution, on fail, runs the request immediately </param>
		/// <param name="bypassPluginExecution">Adds the bypass plugin behavior to this request. Note: this will only apply if the caller has the prvBypassPlugins permission to bypass plugins.  If its attempted without the permission the request will fault.</param>
        /// <param name="serviceClient">ServiceClient</param>
        /// <returns></returns>
        private static bool UpdateStateStatusForEntity(this ServiceClient serviceClient,
            string entName,
            Guid entId,
            string newState,
            string newStatus,
            int newStateid = -1,
            int newStatusid = -1,
            Guid batchId = default(Guid),
            bool bypassPluginExecution = false
            )
        {
            serviceClient._logEntry.ResetLastError();  // Reset Last Error
            SetStateRequest req = new SetStateRequest();
            req.EntityMoniker = new EntityReference(entName, entId);

            int istatuscode = -1;
            int istatecode = -1;

            // Modified to prefer IntID's first... this is in support of multi languages.

            if (newStatusid != -1)
                istatuscode = newStatusid;
            else
            {
                if (!String.IsNullOrWhiteSpace(newStatus))
                {
                    PickListMetaElement picItem = serviceClient.GetPickListElementFromMetadataEntity(entName, "statuscode");
                    if (picItem != null)
                    {
                        var statusOption = picItem.Items.FirstOrDefault(s => s.DisplayLabel.Equals(newStatus, StringComparison.CurrentCultureIgnoreCase));
                        if (statusOption != null)
                            istatuscode = statusOption.PickListItemId;
                    }
                }
            }

            if (newStateid != -1)
                istatecode = newStateid;
            else
            {
                if (!string.IsNullOrWhiteSpace(newState))
                {
                    PickListMetaElement picItem2 = serviceClient.GetPickListElementFromMetadataEntity(entName, "statecode");
                    var stateOption = picItem2.Items.FirstOrDefault(s => s.DisplayLabel.Equals(newState, StringComparison.CurrentCultureIgnoreCase));
                    if (stateOption != null)
                        istatecode = stateOption.PickListItemId;
                }
            }

            if (istatecode == -1 && istatuscode == -1)
            {
                serviceClient._logEntry.Log(string.Format(CultureInfo.InvariantCulture, "Cannot set status on {0}, State and Status codes not found, State = {1}, Status = {2}", entName, newState, newStatus), TraceEventType.Information);
                return false;
            }

            if (istatecode != -1)
                req.State = new OptionSetValue(istatecode);// "Completed";
            if (istatuscode != -1)
                req.Status = new OptionSetValue(istatuscode); //Status = 2;


            if (serviceClient.AddRequestToBatch(batchId, req, string.Format(CultureInfo.InvariantCulture, "Setting Activity State in Dataverse... {0}", entName), string.Format(CultureInfo.InvariantCulture, "Request for SetState on {0} queued", entName), bypassPluginExecution))
                return false;

            SetStateResponse resp = (SetStateResponse)serviceClient.Command_Execute(req, string.Format(CultureInfo.InvariantCulture, "Setting Activity State in Dataverse... {0}", entName), bypassPluginExecution);
            if (resp != null)
                return true;
            else
                return false;
        }

        /// <summary>
        /// Associates one Entity to another where an M2M Relationship Exists.
        /// </summary>
        /// <param name="entityName1">Entity on one side of the relationship</param>
        /// <param name="entity1Id">The Id of the record on the first side of the relationship</param>
        /// <param name="entityName2">Entity on the second side of the relationship</param>
        /// <param name="entity2Id">The Id of the record on the second side of the relationship</param>
        /// <param name="relationshipName">Relationship name between the 2 entities</param>
        /// <param name="batchId">Optional: if set to a valid GUID, generated by the Create Batch Request Method, will assigned the request to the batch for later execution, on fail, runs the request immediately </param>
        /// <param name="bypassPluginExecution">Adds the bypass plugin behavior to this request. Note: this will only apply if the caller has the prvBypassPlugins permission to bypass plugins.  If its attempted without the permission the request will fault.</param>
        /// <param name="serviceClient">ServiceClient</param>
        /// <returns>true on success, false on fail</returns>
        public static bool CreateEntityAssociation(this ServiceClient serviceClient, string entityName1, Guid entity1Id, string entityName2, Guid entity2Id, string relationshipName, Guid batchId = default(Guid), bool bypassPluginExecution = false)
        {
            serviceClient._logEntry.ResetLastError();  // Reset Last Error
            if (serviceClient.DataverseService == null)
            {
                serviceClient._logEntry.Log("Dataverse Service not initialized", TraceEventType.Error);
                return false;
            }

            if (string.IsNullOrEmpty(entityName1) || string.IsNullOrEmpty(entityName2) || entity1Id == Guid.Empty || entity2Id == Guid.Empty)
            {
                serviceClient._logEntry.Log(string.Format(CultureInfo.InvariantCulture, "************ Exception in CreateEntityAssociation, all parameters must be populated"), TraceEventType.Error);
                return false;
            }

            AssociateEntitiesRequest req = new AssociateEntitiesRequest();
            req.Moniker1 = new EntityReference(entityName1, entity1Id);
            req.Moniker2 = new EntityReference(entityName2, entity2Id);
            req.RelationshipName = relationshipName;


            if (serviceClient.AddRequestToBatch(batchId, req, string.Format(CultureInfo.InvariantCulture, "Creating association between({0}) and {1}", entityName1, entityName2),
                    string.Format(CultureInfo.InvariantCulture, "Request to Create association between({0}) and {1} Queued", entityName1, entityName2), bypassPluginExecution))
                return true;

            AssociateEntitiesResponse resp = (AssociateEntitiesResponse)serviceClient.Command_Execute(req, "Executing CreateEntityAssociation", bypassPluginExecution);
            if (resp != null)
                return true;

            return false;
        }

        /// <summary>
        /// Associates multiple entities of the same time to a single entity
        /// </summary>
        /// <param name="targetEntity">Entity that things will be related too.</param>
        /// <param name="targetEntity1Id">ID of entity that things will be related too</param>
        /// <param name="sourceEntityName">Entity that you are relating from</param>
        /// <param name="sourceEntitieIds">ID's of the entities you are relating from</param>
        /// <param name="relationshipName">Name of the relationship between the target and the source entities.</param>
        /// <param name="batchId">Optional: if set to a valid GUID, generated by the Create Batch Request Method, will assigned the request to the batch for later execution, on fail, runs the request immediately </param>
        /// <param name="isReflexiveRelationship">Optional: if set to true, indicates that this is a N:N using a reflexive relationship</param>
		/// <param name="bypassPluginExecution">Adds the bypass plugin behavior to this request. Note: this will only apply if the caller has the prvBypassPlugins permission to bypass plugins.  If its attempted without the permission the request will fault.</param>
        /// <param name="serviceClient">ServiceClient</param>
        /// <returns>true on success, false on fail</returns>
        public static bool CreateMultiEntityAssociation(this ServiceClient serviceClient, string targetEntity, Guid targetEntity1Id, string sourceEntityName, List<Guid> sourceEntitieIds, string relationshipName, Guid batchId = default(Guid), bool bypassPluginExecution = false, bool isReflexiveRelationship = false)
        {
            serviceClient._logEntry.ResetLastError();  // Reset Last Error
            if (serviceClient.DataverseService == null)
            {
                serviceClient._logEntry.Log("Dataverse Service not initialized", TraceEventType.Error);
                return false;
            }

            if (string.IsNullOrEmpty(targetEntity) || string.IsNullOrEmpty(sourceEntityName) || targetEntity1Id == Guid.Empty || sourceEntitieIds == null || sourceEntitieIds.Count == 0)
            {
                serviceClient._logEntry.Log(string.Format(CultureInfo.InvariantCulture, "************ Exception in CreateMultiEntityAssociation, all parameters must be populated"), TraceEventType.Error);
                return false;
            }

            AssociateRequest req = new AssociateRequest();
            req.Relationship = new Relationship(relationshipName);
            if (isReflexiveRelationship) // used to determine if the relationship role is reflexive.
                req.Relationship.PrimaryEntityRole = EntityRole.Referenced;
            req.RelatedEntities = new EntityReferenceCollection();
            foreach (Guid g in sourceEntitieIds)
            {
                req.RelatedEntities.Add(new EntityReference(sourceEntityName, g));
            }
            req.Target = new EntityReference(targetEntity, targetEntity1Id);

            if (serviceClient.AddRequestToBatch(batchId, req, string.Format(CultureInfo.InvariantCulture, "Creating multi association between({0}) and {1}", targetEntity, sourceEntityName),
                    string.Format(CultureInfo.InvariantCulture, "Request to Create multi association between({0}) and {1} queued", targetEntity, sourceEntityName), bypassPluginExecution))
                return true;

            AssociateResponse resp = (AssociateResponse)serviceClient.Command_Execute(req, "Executing CreateMultiEntityAssociation", bypassPluginExecution);
            if (resp != null)
                return true;

            return false;
        }

        /// <summary>
        /// Removes the Association between 2 entity items where an M2M Relationship Exists.
        /// </summary>
        /// <param name="entityName1">Entity on one side of the relationship</param>
        /// <param name="entity1Id">The Id of the record on the first side of the relationship</param>
        /// <param name="entityName2">Entity on the second side of the relationship</param>
        /// <param name="entity2Id">The Id of the record on the second side of the relationship</param>
        /// <param name="relationshipName">Relationship name between the 2 entities</param>
        /// <param name="batchId">Optional: if set to a valid GUID, generated by the Create Batch Request Method, will assigned the request to the batch for later execution, on fail, runs the request immediately </param>
		/// <param name="bypassPluginExecution">Adds the bypass plugin behavior to this request. Note: this will only apply if the caller has the prvBypassPlugins permission to bypass plugins.  If its attempted without the permission the request will fault.</param>
        /// <param name="serviceClient">ServiceClient</param>
        /// <returns>true on success, false on fail</returns>
        public static bool DeleteEntityAssociation(this ServiceClient serviceClient, string entityName1, Guid entity1Id, string entityName2, Guid entity2Id, string relationshipName, Guid batchId = default(Guid), bool bypassPluginExecution = false)
        {
            serviceClient._logEntry.ResetLastError();  // Reset Last Error
            if (serviceClient.DataverseService == null)
            {
                serviceClient._logEntry.Log("Dataverse Service not initialized", TraceEventType.Error);
                return false;
            }

            if (string.IsNullOrEmpty(entityName1) || string.IsNullOrEmpty(entityName2) || entity1Id == Guid.Empty || entity2Id == Guid.Empty)
            {
                serviceClient._logEntry.Log(string.Format(CultureInfo.InvariantCulture, "************ Exception in DeleteEntityAssociation, all parameters must be populated"), TraceEventType.Error);
                return false;
            }

            DisassociateEntitiesRequest req = new DisassociateEntitiesRequest();
            req.Moniker1 = new EntityReference(entityName1, entity1Id);
            req.Moniker2 = new EntityReference(entityName2, entity2Id);
            req.RelationshipName = relationshipName;

            if (serviceClient.AddRequestToBatch(batchId, req, string.Format(CultureInfo.InvariantCulture, "Executing DeleteEntityAssociation between ({0}) and {1}", entityName1, entityName2),
                              string.Format(CultureInfo.InvariantCulture, "Request to Execute DeleteEntityAssociation between ({0}) and {1} Queued", entityName1, entityName2), bypassPluginExecution))
                return true;

            DisassociateEntitiesResponse resp = (DisassociateEntitiesResponse)serviceClient.Command_Execute(req, "Executing DeleteEntityAssociation", bypassPluginExecution);
            if (resp != null)
                return true;

            return false;
        }

    }
}
