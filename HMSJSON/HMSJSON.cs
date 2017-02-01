﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Configuration;
using Newtonsoft.Json;

namespace HMSJSON
{
    public class HMSJSON
    {
        public string timeseriesJSON { get; set; }
        public string metadataJSON { get; set; }
        public string datasetJSON { get; set; }
        public string sourceJSON { get; set; }
        public string newMetadataJSON { get; set; }
        private Dictionary<string, string> metaDictionary { get; set; }
        
        public struct HMSData
        {
            public string dataset;
            public string source;
            public Dictionary<string, string> metadata;
            public Dictionary<string, List<string>> data;
        };

        /// <summary>
        /// Returns a space separated list of data as a json format string. OBSOLETE
        /// </summary>
        /// <param name="errorMsg"></param>
        /// <param name="data"></param>
        /// <param name="dataset"></param>
        /// <returns></returns>
        public string ConvertDataToJSON(out string errorMsg, string data, string dataset)
        {
            errorMsg = "";
            string[] dataLine = data.Split(new string[] { "\n" }, StringSplitOptions.RemoveEmptyEntries);
            string jsonString = "";

            jsonString = String.Concat("{\"", dataset,"\":[");

            for (int i = 0; i < dataLine.Length; i++)
            {
                string[] dataArray = dataLine[i].Split(new string[] { " " }, StringSplitOptions.RemoveEmptyEntries);

                //Explicitly stating data types
                //jsonString += "{\"date/hour\":\"" + dataArray[0] + " " + dataArray[1] + "\",";
                //jsonString += "\"data\":\"" + dataArray[2] + "\"}";

                //Implicitly stating data types
                jsonString = String.Concat(jsonString, "{\"", dataArray[0], " ", dataArray[1], "\":\"", dataArray[2], "\"}");
                //jsonString += @"{""" + dataArray[0] + " " + dataArray[1] + @""":""" + dataArray[2] + @"""}";

                if (i+3 != dataLine.Length)
                {
                    jsonString += @",";
                }
                i += 2;
            }
            jsonString += @"]}";
            int count = Encoding.ASCII.GetCharCount(Encoding.ASCII.GetBytes(jsonString));
            return jsonString;
        }

        /// <summary>
        /// Returns a single string containing all the individual timeseries data with their metadata.
        /// </summary>
        /// <param name="errorMsg"></param>
        /// <param name="ts"></param>
        /// <param name="dataset"></param>
        /// <returns></returns>
        public string CombineDatasetsAsJSON(out string errorMsg, List<HMSTimeSeries.HMSTimeSeries> ts, string dataset, string source, bool localtime, double gmtOffset)
        {
            errorMsg = "";
            //string data = "";
            StringBuilder stBuilder = new StringBuilder();
            for (int i = 0; i < ts.Count; i++)
            {
                // "DataSetBlock" is used to parse the data on the client side, where it is saved as individual files.
                string jsonString = GetJSONString(out errorMsg, ts[i].timeSeries, ts[i].newMetaData, ts[i].metaData, dataset, source, localtime, gmtOffset);
                stBuilder.Append("DataSetBlock");
                stBuilder.Append(jsonString);
            }
            return stBuilder.ToString();
        }

        /// <summary>
        /// Generates a HMSData object from the argument parameters and converts the object into a json string.
        /// </summary>
        /// <param name="errorMsg"></param>
        /// <param name="timeseries"></param>
        /// <param name="newMetaData"></param>
        /// <param name="metadata"></param>
        /// <param name="dataset"></param>
        /// <param name="source"></param>
        /// <param name="localtime"></param>
        /// <param name="gmtOffset"></param>
        /// <returns></returns>
        public string GetJSONString(out string errorMsg, string timeseries, string newMetaData, string metadata, string dataset, string source, bool localtime, double gmtOffset)
        {
            errorMsg = "";
            HMSData output = new HMSData();
            output.dataset = dataset;
            output.source = source;
            if (source.Contains("NLDAS") || source.Contains("GLDAS"))
            {
                output.metadata = SetHMSDataMetaData(out errorMsg, newMetaData, metadata);
                output.data = SetHMSDataTS(out errorMsg, timeseries, localtime, gmtOffset);
            }
            else if (source.Contains("Daymet"))
            {
                output.metadata = SetHMSDaymetMetaData(out errorMsg, newMetaData, metadata);
                output.data = SetHMSDaymetDataTS(out errorMsg, timeseries);
            }
            else
            {
                errorMsg = "Error: Unable to create JSON from data.";
                return null;
            }
            return JsonConvert.SerializeObject(output);
        }

        /// <summary>
        /// Creates a dictionary from the input metadata.
        /// </summary>
        /// <param name="errorMsg"></param>
        /// <param name="output"></param>
        /// <param name="newMetaData"></param>
        /// <param name="metadata"></param>
        /// <returns></returns>
        private Dictionary<string, string> SetHMSDataMetaData(out string errorMsg, string newMetaData, string metadata)
        {
            errorMsg = "";
            Dictionary<string, string> metaDict = new Dictionary<string, string>();

            string[] keys = ConfigurationManager.AppSettings["metadataConfig"].ToString().Split(',');
            string[] metaLines = metadata.Split(new string[] { "\n" }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = metaLines.Length - 1; i > 0; i--)
            {
                string[] metadataLineArray = metaLines[i].Split(new char[] { '=' }, 2);
                if (keys.Contains(metadataLineArray[0]) && !metaDict.ContainsKey(metadataLineArray[0]) && !metaDict.Keys.Contains(metadataLineArray[0] + "[GMT]"))
                {
                    if (metadataLineArray[0].Contains("begin_time") || metadataLineArray[0].Contains("end_time"))
                    {
                        metaDict.Add(metadataLineArray[0] + "[GMT]" , metadataLineArray[1].Trim());
                    }
                    else
                    {
                        metaDict.Add(metadataLineArray[0], metadataLineArray[1].Trim());
                    }
                }
            }
            string[] dataLines = newMetaData.Split(new string[] { "\n" }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < dataLines.Length; i++)
            {
                if (dataLines[i].Contains("="))
                {
                    string[] line = dataLines[i].Split(new char[] { '=' }, 2);
                    if (!metaDict.ContainsKey(line[0]))
                    {
                        metaDict.Add(line[0], line[1].Trim());
                    }
                }
            }
            return metaDict;
        }

        /// <summary>
        /// Creates the timeseries dictionary containing the date/hour as the key and an array for the data values, depending on the total number of datasets provided.
        /// </summary>
        /// <param name="errorMsg"></param>
        /// <param name="output"></param>
        /// <param name="timeseries"></param>
        /// <param name="localtime"></param>
        /// <param name="gmtOffset"></param>
        /// <returns></returns>
        private Dictionary<string, List<string>> SetHMSDataTS(out string errorMsg, string timeseries, bool localtime, double gmtOffset)
        {
            errorMsg = "";
            Dictionary<string, List<string>> data = new Dictionary<string, List<string>>();
            List<string> timestepData;
            string[] tsLines = timeseries.Split(new string[] { "\n" }, StringSplitOptions.RemoveEmptyEntries);
            HMSGDAL.HMSGDAL gdal = new HMSGDAL.HMSGDAL();
            for (int i = 0; i < tsLines.Length; i++)
            {
                timestepData = new List<string>();
                string date = "";
                string[] lineData = tsLines[i].Split(new string[] { " " }, StringSplitOptions.RemoveEmptyEntries);

                if (localtime == true) { date = gdal.SetDateToLocal(out errorMsg, lineData[0] + " " + lineData[1], gmtOffset); }
                else { date = lineData[0] + " " + lineData[1]; }
                string values = "";
                for (int j = 2; j < lineData.Length; j++)
                {
                    values = values + lineData[j];
                    if (j != lineData.Length - 1) { values += ", "; }
                }
                timestepData.Add(values);
                data.Add(date, timestepData);
            }
            return data;
        }

        /// <summary>
        /// Creates the metaData dictionary for Daymet data.
        /// </summary>
        /// <param name="errorMsg"></param>
        /// <param name="output"></param>
        /// <param name="newMetaData"></param>
        /// <param name="metadata"></param>
        /// <returns></returns>
        private Dictionary<string, string> SetHMSDaymetMetaData(out string errorMsg, string newMetaData, string metadata)
        {
            errorMsg = "";
            Dictionary<string, string> daymetMeta = new Dictionary<string, string>();
            string[] metaLines = metadata.Split(new string[] { "\n", "  " }, StringSplitOptions.RemoveEmptyEntries);
            for(int i = 0; i < metaLines.Length; i++)
            {
                if (metaLines[i].Contains("http"))
                {
                    daymetMeta.Add("url_reference:", metaLines[i].Trim());
                }
                else if(metaLines[i].Contains(':'))
                {
                    string[] lineData = metaLines[i].Split(new string[] { ": " }, StringSplitOptions.RemoveEmptyEntries);
                    daymetMeta.Add(lineData[0].Trim(), lineData[1].Trim());
                }
            }
            if (newMetaData == null) { return daymetMeta; }
            string[] dataLines = newMetaData.Split(new string[] { "\n" }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < dataLines.Length; i++)
            {
                if (dataLines[i].Contains("="))
                {
                    string[] line = dataLines[i].Split(new char[] { '=' }, 2);
                    if (!daymetMeta.ContainsKey(line[0]))
                    {
                        daymetMeta.Add(line[0], line[1].Trim());
                    }
                }
            }
            return daymetMeta;
        }

        /// <summary>
        /// Creates the timeseries dictionary for the daymet data.
        /// </summary>
        /// <param name="errorMsg"></param>
        /// <param name="output"></param>
        /// <param name="timeseries"></param>
        /// <returns></returns>
        private Dictionary<string, List<string>> SetHMSDaymetDataTS(out string errorMsg, string timeseries)
        {
            errorMsg = "";
            Dictionary<string, List<string>> data = new Dictionary<string, List<string>>();
            string[] tsLines = timeseries.Split(new string[] { "\n" }, StringSplitOptions.RemoveEmptyEntries);
            for(int i = 0; i < tsLines.Length; i++)
            {
                string[] lineData = tsLines[i].Split(new string[] { " " }, StringSplitOptions.RemoveEmptyEntries);
                data.Add(lineData[0] + " " + lineData[1], new List<string> { lineData[2] });
            }
            return data;
        }

        /// <summary>
        /// Returns a single string containing all the retrieved timeseries data, merged together. The metadata for the location and elevation of each point is added to the output metadata.
        /// </summary>
        /// <param name="errorMsg"></param>
        /// <param name="ts"></param>
        /// <param name="dataset"></param>
        /// <returns></returns>
        public string CombineTimeseriesAsJson(out string errorMsg, List<HMSTimeSeries.HMSTimeSeries> ts, string dataset, string source, bool localtime, double gmtOffset)
        {
            errorMsg = "";
            HMSData json = new HMSData();
            if (source.Contains("GLDAS") || source.Contains("NLDAS"))
            {
                json.data = SetHMSDataTS(out errorMsg, ts[0].timeSeries, localtime, gmtOffset);
                json.metadata = SetHMSDataMetaData(out errorMsg, ts[0].newMetaData, ts[0].metaData);

                if (ts.Count > 1)
                {
                    json.metadata.Add("timeseries_1", ts[0].metaLat + "," + ts[0].metaLon);
                    json.metadata.Add("elevation[m]_1", ts[0].metaElev.ToString());
                    json.metadata.Add("percentInCell_1", json.metadata["percentInCell"]);
                    json.metadata.Remove("elevation[m]");
                }
                else
                {
                    json.metadata.Add("timeseries", ts[0].metaLat + "," + ts[0].metaLon);
                }
                for (int i = 1; i < ts.Count; i++)
                {
                    Dictionary<string, string> tempMeta = SetHMSDataMetaData(out errorMsg, ts[i].newMetaData, ts[i].metaData);
                    json.metadata.Add("timeseries_" + (i + 1), ts[i].metaLat + "," + ts[i].metaLon);
                    json.metadata.Add("elevation[m]_" + (i + 1), ts[i].metaElev.ToString());
                    if (ts.Count > 1) { json.metadata.Add("percentInCell_" + (i + 1), tempMeta["percentInCell"]); }
                    Dictionary<string, List<string>> values = MergeJsonTS(out errorMsg, json.data, ts[i].timeSeries);
                    json.data = values;
                }
            }
            else if (source.Contains("Daymet"))
            {
                json.metadata = SetHMSDaymetMetaData(out errorMsg, ts[0].newMetaData, ts[0].metaData);
                json.data = SetHMSDaymetDataTS(out errorMsg, ts[0].timeSeries);
            }
            json.dataset = dataset;
            json.source = source;
            return JsonConvert.SerializeObject(json);
        }
        
        /// <summary>
        /// Takes an existing timeseries and adds on to the data array for each time entry.
        /// </summary>
        /// <param name="errorMsg"></param>
        /// <param name="baseTS"></param>
        /// <param name="newTS"></param>
        /// <returns></returns>
        private Dictionary<string, List<string>> MergeJsonTS(out string errorMsg, Dictionary<string, List<string>> baseTS, string newTS)
        {
            errorMsg = "";
            string[] newTSLines = newTS.Split(new string[] { "\n" }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < newTSLines.Length; i++ )
            {
                string[] lineData = newTSLines[i].Split(new string[] { " " }, StringSplitOptions.RemoveEmptyEntries);
                string values = "";
                for (int j = 2; j < lineData.Length; j++)
                {
                    values = values + lineData[j];
                    if (j != lineData.Length - 1) { values += ", "; }
                }
                baseTS[lineData[0] + " " + lineData[1]].Add(values);
            }
            return baseTS;
        }
    }
}
