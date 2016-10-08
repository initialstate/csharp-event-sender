using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Reflection;
using log4net;
using RestSharp;

namespace InitialState.Events
{
    public class ISEventSender : IISEventSender
    {
        private readonly InitialStateConfig _config;
        private readonly ILog _log = LogManager.GetLogger("is_event_sender");
        private readonly string _version;
        private readonly DateTime epochDateTime = new DateTime(1970, 1, 1);


        public ISEventSender()
        {
            _version =
                FileVersionInfo.GetVersionInfo(Assembly.GetExecutingAssembly().Location).ProductVersion;
            _config = (InitialStateConfig) ConfigurationManager.GetSection("initialstate");
        }

        public void Send(string key, string value, string bucketKey = null, DateTime? timestamp = null,
            bool sendAsync = true)
        {
            IDictionary<string, string> dict = new Dictionary<string, string>
                                               {
                                                   {key, value}
                                               };

            Send(dict, bucketKey, timestamp);
        }

        public void Send<T>(T obj, string bucketKey = null, DateTime? timestamp = null, bool sendAsync = true)
        {
            if (string.IsNullOrEmpty(bucketKey))
                bucketKey = _config.DefaultBucketKey;

            double epoch = GetEpoch(timestamp);

            var client = new RestClient(_config.ApiBase);
            client.UserAgent = "initialstate_core_api/" + _version;

            var request = new RestRequest("/events", Method.POST);
            request.AddHeader("X-IS-AccessKey", _config.AccessKey);
            request.AddHeader("X-IS-BucketKey", bucketKey);
            request.AddHeader("Accept-Version", "0.0.4");
            request.JsonSerializer = new JsonSerializer();


            if (typeof (T).IsAssignableFrom(typeof (IDictionary<string, string>)))
            {
                List<Event> events = ((IDictionary<string, string>) obj).Select(kvp => new Event
                                                                                       {
                                                                                           Epoch = epoch,
                                                                                           Key = kvp.Key,
                                                                                           Value = kvp.Value
                                                                                       }).ToList();
                request.AddJsonBody(events);
            }
            else
            {
                PropertyInfo[] properties = typeof (T).GetProperties();
                List<Event> events = properties.Select(prop => new Event
                                                               {
                                                                   Epoch = epoch,
                                                                   Key = prop.Name,
                                                                   Value = prop.GetValue(obj).ToString()
                                                               }).ToList();
                request.AddJsonBody(events);
            }

            Parameter bodyRequest = request.Parameters.FirstOrDefault(x => x.Type == ParameterType.RequestBody);
            string body = "<empty>";
            if (bodyRequest != null)
            {
                body = bodyRequest.Value.ToString();
            }

            if (sendAsync)
            {
                client.ExecuteAsync(request, response =>
                                             {
                                                 if ((int) response.StatusCode > 299 || (int) response.StatusCode < 200)
                                                 {
                                                     _log.ErrorFormat(
                                                         "Unsuccessfully submitted events to {2}... {0} {1}",
                                                         response.StatusCode, body,
                                                         response.ResponseUri);
                                                 }
                                             });
            }
            else
            {
                IRestResponse response = client.Execute(request);

                if ((int) response.StatusCode > 299 || (int) response.StatusCode < 200)
                {
                    _log.ErrorFormat("Unsuccessfully submitted events to {2}... {0} {1}", response.StatusCode, body,
                        response.ResponseUri);

                    throw new SendException(response.StatusCode, body, response.Content);
                }
            }
        }

        private double GetEpoch(DateTime? timestamp)
        {
            if (timestamp == null)
                timestamp = DateTime.UtcNow;

            return timestamp.Value.Subtract(epochDateTime).TotalMilliseconds/1000;
        }

        private class Event
        {
            public string Key { get; set; }
            public string Value { get; set; }
            public double Epoch { get; set; }
        }

        public class SendException : Exception
        {
            public SendException(HttpStatusCode statusCode, string body, string responseContent)
            {
                StatusCode = statusCode;
                Body = body;
                ResponseContent = responseContent;
            }

            public HttpStatusCode StatusCode { get; set; }
            public string Body { get; set; }
            public string ResponseContent { get; set; }
        }
    }
}