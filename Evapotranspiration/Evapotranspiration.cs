﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HMSEvapotranspiration
{
    public class Evapotranspiration : HMSLDAS.IHMSModule
    {

        // Class variables which define a Evapotranspiration object.
        public double latitude { get; set; }                            // Latitude for timeseries
        public double longitude { get; set; }                           // Longitude for timeseries
        public DateTime startDate { get; set; }                         // Start data for timeseries
        public DateTime endDate { get; set; }                           // End date for timeseries
        public double gmtOffset { get; set; }                           // Timezone offset from GMT
        public string tzName { get; set; }                              // Timezone name
        public string dataSource { get; set; }                          // NLDAS, GLDAS, or SWAT algorithm simulation
        public bool localTime { get; set; }                             // False = GMT time, true = local time.
        public List<HMSTimeSeries.HMSTimeSeries> ts { get; set; }       // TimeSeries Data
        public double cellWidth { get; set; }                           // LDAS data point cell width, defined by source.
        public string shapefilePath { get; set; }                       // Path to shapefile, if provided. Used in place of coordinates.
        public HMSGDAL.HMSGDAL gdal { get; set; }                       // GDAL object for GIS operations.
        public HMSJSON.HMSJSON.HMSData jsonData { get; set; }

        public Evapotranspiration() { }

        /// <summary>
        /// Constructor using a shapefile.
        /// </summary>
        /// <param name="errorMsg"></param>
        /// <param name="startDate"></param>
        /// <param name="endDate"></param>
        /// <param name="source"></param>
        /// <param name="local"></param>
        /// <param name="snow"></param>
        public Evapotranspiration(out string errorMsg, string startDate, string endDate, string source, bool local, string sfPath) : this(out errorMsg, "0.0", "0.0", startDate, endDate, source, local, sfPath)
        {
        }

        /// <summary>
        /// Constructor using latitude and longitude.
        /// </summary>
        /// <param name="errorMsg"></param>
        /// <param name="latitude"></param>
        /// <param name="longitude"></param>
        /// <param name="startDate"></param>
        /// <param name="endDate"></param>
        /// <param name="source"></param>
        /// <param name="local"></param>
        /// <param name="sfPath"></param>
        public Evapotranspiration(out string errorMsg, string latitude, string longitude, string startDate, string endDate, string source, bool local, string sfPath) : this(out errorMsg, latitude, longitude, startDate, endDate, source, local, sfPath, "0.0", "NaN")
        {
        }

        /// <summary>
        /// Constructor using latitude and longitude. with gmtOffset already known.
        /// </summary>
        /// <param name="errorMsg"></param>
        /// <param name="latitude"></param>
        /// <param name="longitude"></param>
        /// <param name="startDate"></param>
        /// <param name="endDate"></param>
        /// <param name="source"></param>
        /// <param name="local"></param>
        /// <param name="sfPath"></param>
        public Evapotranspiration(out string errorMsg, string latitude, string longitude, string startDate, string endDate, string source, bool local, string sfPath, string gmtOffset, string tzName)
        {
            errorMsg = "";
            this.gmtOffset = Convert.ToDouble(gmtOffset);
            this.dataSource = source;
            this.localTime = local;
            this.tzName = tzName;
            if (errorMsg.Contains("Error")) { return; }
            SetDates(out errorMsg, startDate, endDate);
            if (errorMsg.Contains("Error")) { return; }
            ts = new List<HMSTimeSeries.HMSTimeSeries>();
            if (string.IsNullOrWhiteSpace(sfPath))
            {
                this.shapefilePath = null;
                this.latitude = ConvertStringToDouble(out errorMsg, latitude);
                this.longitude = ConvertStringToDouble(out errorMsg, longitude);
            }
            else
            {
                this.shapefilePath = sfPath;
                this.latitude = 0.0;
                this.longitude = 0.0;
            }
            if (this.dataSource == "NLDAS") { this.cellWidth = 0.12500; }
            else if (this.dataSource == "GLDAS") { this.cellWidth = 0.2500; }
            this.gdal = new HMSGDAL.HMSGDAL();
        }

        /// <summary>
        /// Method first checks if the string is numeric then attemps to convert to a double.
        /// </summary>
        /// <param name="errorMsg"></param>
        /// <param name="str"></param>
        /// <returns></returns>
        private double ConvertStringToDouble(out string errorMsg, string str)
        {
            errorMsg = "";
            double result = 0.0;
            if (Double.TryParse(str, out result))
            {
                try
                {
                    return result = Convert.ToDouble(str);
                }
                catch
                {
                    errorMsg = "Error: Unable to convert string value to double.";
                    return result;
                }
            }
            else
            {
                errorMsg = "Error: Coordinates contain invalid characters.";
                return result;
            }
        }

        /// <summary>
        /// Sets startDate and endDate, checks that dates are valid (start date before end date, end date no greater than today, start dates are valid for data sources)
        /// </summary>
        /// <param name="errorMsg"></param>
        /// <param name="start"></param>
        /// <param name="end"></param>
        private void SetDates(out string errorMsg, string start, string end)
        {
            errorMsg = "";
            try
            {
                this.startDate = Convert.ToDateTime(start);
                this.endDate = Convert.ToDateTime(end);
                //Add Hours to start/end dates
                DateTime newStartDate = new DateTime(this.startDate.Year, this.startDate.Month, this.startDate.Day, 00, 00, 00);
                DateTime newEndDate = new DateTime(this.endDate.Year, this.endDate.Month, this.endDate.Day, 23, 00, 00);
                this.startDate = newStartDate;
                this.endDate = newEndDate;
            }
            catch
            {
                errorMsg = "Error: Invalid data format. Please provide a date as mm-dd-yyyy or mm/dd/yyyy.";
                return;
            }
            if (DateTime.Compare(this.endDate, DateTime.Today) > 0)   //If endDate is past today's date, endDate is set to 2 days prior to today.
            {
                this.endDate = DateTime.Today.AddDays(-2.0);
            }
            if (DateTime.Compare(this.startDate, this.endDate) > 0)
            {
                errorMsg = "Error: Invalid dates entered. Please enter an end date set after the start date.";
                return;
            }
            if (this.dataSource.Contains("NLDAS"))   //NLDAS data collection start date
            {
                DateTime minDate = new DateTime(1979, 01, 02);
                if (DateTime.Compare(this.startDate, minDate) < 0)
                {
                    this.startDate = minDate;   //start date is set to NLDAS start date
                }
            }
            else if (this.dataSource.Contains("GLDAS"))   //GLDAS data collection start date
            {
                DateTime minDate = new DateTime(2000, 02, 25);
                if (DateTime.Compare(this.startDate, minDate) < 0)
                {
                    this.startDate = minDate;   //start date is set to GLDAS start date
                }
            }
        }

        /// <summary>
        /// Gets precipitation data.
        /// </summary>
        /// <param name="errorMsg"></param>
        public List<HMSTimeSeries.HMSTimeSeries> GetDataSets(out string errorMsg)
        {
            errorMsg = "";
            HMSLDAS.HMSLDAS gldas = new HMSLDAS.HMSLDAS();
            double offset = gmtOffset;
            HMSTimeSeries.HMSTimeSeries newTS = new HMSTimeSeries.HMSTimeSeries();
            ts.Add(newTS);

            if (this.shapefilePath != null)
            {
                bool sourceNLDAS = true;
                if (this.dataSource.Contains("GLDAS")) { sourceNLDAS = false; }
                double[] center = gldas.DetermineReturnCoordinates(out errorMsg, gdal.ReturnCentroid(out errorMsg, this.shapefilePath), sourceNLDAS);
                this.latitude = center[0];   // coordinate values for Precipitation objects are taken from the centroid of the shapefile.
                this.longitude = center[1];
                //gdal.CellAreaInShapefile(out errorMsg, center, this.cellWidth);               //Obsolete
                gdal.CellAreaInShapefileByGrid(out errorMsg, center, this.cellWidth);
                if (errorMsg.Contains("Error")) { return null; }
            }

            if (this.localTime == true && tzName.Contains("NaN"))
            {
                this.gmtOffset = gdal.GetGMTOffset(out errorMsg, this.latitude, this.longitude, ts[0]);    //Gets the GMT offset
                if (errorMsg.Contains("Error")) { return null; }
                this.tzName = ts[0].tzName;              //Gets the Timezone name
                if (errorMsg.Contains("Error")) { return null; }
                this.startDate = gdal.AdjustDateByOffset(out errorMsg, this.gmtOffset, this.startDate, true);
                this.endDate = gdal.AdjustDateByOffset(out errorMsg, this.gmtOffset, this.endDate, false);
            }

            gldas.BeginLDASSequence(out errorMsg, this, "Evapotrans", newTS);
            HMSJSON.HMSJSON output = new HMSJSON.HMSJSON();
            this.jsonData = output.ConstructHMSDataFromTS(out errorMsg, this.ts, "Evapotranspiration", this.dataSource, this.localTime, this.gmtOffset);
            if (errorMsg.Contains("Error")) { return null; }
            return ts;
        }

        /// <summary>
        /// Gets the precipitaiton data using a shapefile.
        /// </summary>
        /// <param name="errorMsg"></param>
        /// <returns></returns>
        public string GetDataSetsString(out string errorMsg)
        {
            errorMsg = "";
            GetDataSets(out errorMsg);
            if (errorMsg.Contains("Error")) { return null; }
            HMSJSON.HMSJSON output = new HMSJSON.HMSJSON();
            string combinedData = output.CombineTimeseriesAsJson(out errorMsg, this.jsonData);
            if (errorMsg.Contains("Error")) { return null; }
            return combinedData;
        }
    }
}