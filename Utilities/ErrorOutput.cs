﻿using Data;
using System;
using System.Collections.Generic;
using System.Text;

namespace Utilities
{
    public class ErrorOutput : ITimeSeriesOutput
    {

        // Implement ITimeSeries interface
        public string Dataset { get; set; }
        public string DataSource { get; set; }
        public Dictionary<string, string> Metadata { get; set; }
        public Dictionary<string, List<string>> Data { get; set; }

        /// <summary>
        /// Default ErrorOutput constructor
        /// </summary>
        public ErrorOutput()
        {
            this.Dataset = "";
            this.DataSource = "";
            this.Metadata = new Dictionary<string, string>();
            this.Data = new Dictionary<string, List<string>>();
        }

        /// <summary>
        /// Returns ITimeSeries object containing the errorMsg in the metadata.
        /// </summary>
        /// <param name="errorMsg">Error message to be pasted to the MetaData in the output.</param>
        /// <returns>ITimeSeries</returns>
        public ITimeSeriesOutput ReturnError(string errorMsg)
        {
            ITimeSeriesOutput output = new ErrorOutput();
            output.Metadata["ERROR"] = errorMsg;
            return output;
        }

        /// <summary>
        /// Returns ITimeSeries object containing the errorMsg in the metadata.
        /// </summary>
        /// <param name="errorMsg">Error message to be pasted to the MetaData in the output.</param>
        /// <returns>ITimeSeries</returns>
        public ITimeSeriesOutput ReturnError(string dataset, string source, string errorMsg)
        {
            ITimeSeriesOutput output = new ErrorOutput
            {
                Dataset = dataset,
                DataSource = source
            };
            output.Metadata[source + "_ERROR"] = errorMsg;
            return output;
        }



    }
}
