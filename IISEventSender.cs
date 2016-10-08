using System;
using System.Collections.Generic;

namespace InitialState.Events
{
    public interface IISEventSender
    {
        void Send(string key, string value, string bucketKey = null, DateTime? timestamp = null, bool sendAsync = true);
        void Send<T>(T obj, string bucketKey = null, DateTime? timestamp = null, bool sendAsync = true);
    }
}
