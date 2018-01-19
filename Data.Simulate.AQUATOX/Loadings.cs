using System;
using System.IO;
using System.Collections.Generic;
using System.Runtime.Serialization;

namespace AQUATOX.Loadings
{

    public static class BinarySearch4All
    {
        public static int BinarySearch<TKey, TValue>(this SortedList<TKey, TValue> sortedList,
                TKey value, IComparer<TKey> comparer = null)
        {
            return BinarySearch(sortedList, 0, sortedList.Count, value, comparer);
        }

        public static int BinarySearch<TKey, TValue>(this SortedList<TKey, TValue> sortedList,
                int index, int length, TKey value, IComparer<TKey> comparer = null)
        {
            return BinarySearch(sortedList.Keys, index, length, value, comparer);
        }

        public static int BinarySearch<T>(this IList<T> list, T value, IComparer<T> comparer = null)
        {
            return BinarySearch(list, 0, list.Count, value, comparer);
        }

        // The zero-based index of item in the sorted List<T>, if item is found; otherwise, a negative number that is the bitwise complement 
        // of the index of the next element that is larger than item or, if there is no larger element, the bitwise complement of Count.
        // algorithm courtesy of http://referencesource.microsoft.com/#mscorlib/system/collections/generic/arraysorthelper.cs#114ea99d8baee1be
        public static int BinarySearch<T>(this IList<T> list, int index, int length,
                T value, IComparer<T> comparer = null)
        {
            if (comparer == null)
                comparer = Comparer<T>.Default;
            int lo = index;
            int hi = index + length - 1;
            while (lo <= hi)
            {
                int i = lo + ((hi - lo) >> 1);
                int order = comparer.Compare(list[i], value);

                if (order == 0) return i;
                if (order < 0)
                    lo = i + 1;
                else
                    hi = i - 1;
            }
            return ~lo;
        }
    }



    [Serializable]
    [KnownType(typeof(TLoadings))]
    [DataContract]
    public class TLoadings

        {
        [DataMember] public SortedList<DateTime, double> list = new SortedList<DateTime, double>();
        [DataMember] public bool Hourly = false;
        [DataMember] public double ConstLoad = 0;         // User Input constant load
        [DataMember] public bool UseConstant = true;      // Flag for using constant load
        [DataMember] public bool NoUserLoad = false;      // Flag for using user input load, or ignoring  the load and using annual ranges and means.  Relevant to Temp, Light, pH, and Nutrients
        [DataMember] public double MultLdg = 1;           // to perturb loading
        [IgnoreDataMember] int lastindexread = -1;

        public double ReturnLoad(DateTime TimeIndex)
        {

            double RetLoad;
            DateTime TIHolder;

            if (UseConstant)
            {
                RetLoad = ConstLoad;
            }
            else
            {
                RetLoad = 0;
                bool foundopt = false;
                if ((list != null)&&(list.Count!=0))
                {
                    if (lastindexread > -1)
                    {
                        if (list.Keys[lastindexread] == TimeIndex) { RetLoad = list.Values[lastindexread]; foundopt = true; }
                        else if (lastindexread < list.Count - 2)
                        {
                            if (list.Keys[lastindexread + 1] == TimeIndex) { RetLoad = list.Values[lastindexread + 1]; foundopt = true; lastindexread = lastindexread + 1; }
                        }
                    }
                    if (!foundopt)
                    {
                        TIHolder = TimeIndex;
                        if (!Hourly) TIHolder = TimeIndex.Date;

                        int indx = list.BinarySearch(TIHolder);
                        if (indx >= 0)
                             { RetLoad = list.Values[indx]; lastindexread = indx; }
                        else
                        {
                            //                       This procedure calculates the time-series loading for a given timeindex.
                            //                         If there is only one time-series loading point present, that is essentially
                            //                         the same thing as a constant load, no further data are available.
                            //
                            //                         If there is more than one point, then the procedure linearly interpolates
                            //                         the correct loading for the time index.A year cycle is also assumed for
                            //                         loading.If the timeindex is between two points, a straight linear interpolation
                            //                         is done.If the timeindex is not between two points,
                            //                         it is moved forward or backward by 1 year increments to try
                            //                         and get it between two points.  If necessary another "dummy" loading is added
                            //                         after the timeindex to make interpolation possible. (if jumping does not
                            //                         place the point within two points}

                            if (list.Count == 0) RetLoad = 0;
                            else
                            {

              //   Four Cases,  (1) TimeIndex before all loading dates,
                            //  (2) TimeIndex after all loading dates,
                            //  (3) TimeIndex in the middle of 2 loading dates
                            //  (4) TimeIndex = Loading Date.}

                                if (indx == ~list.Count) // {case 2}
                                    do  //  Move TimeIndex back to create case 1,3, or 4
                                    {
                                        TIHolder = TIHolder.AddYears(-1);
                                        indx = list.BinarySearch(TIHolder);
                                    }
                                    while (indx == ~list.Count);

                                //     Try to Translate Case 1 into Case 3 or 4 by Moving TimeIndex up.
                                //     May jump all loadings and create case 2}

                                if (indx == -1)
                                    do                                     // Move TimeIndex forward to create case 2,3, or 4}
                                    {
                                        TIHolder = TIHolder.AddYears(1);
                                        indx = list.BinarySearch(TIHolder);
                                    }
                                    while (indx == -1);

                                if (indx == ~list.Count)  // Jumped to Case 2, Need to Add "New" Load at end for interpolation
                                    do
                                    {
                                        list.Add(list.Keys[0].AddYears(1), list.Values[0]);
                                        indx = list.BinarySearch(TIHolder);
                                    }
                                    while (indx == ~list.Count);

                                if (indx>=0)
                                     { RetLoad = list.Values[indx]; lastindexread = indx; }
                                else
                                {
                                    indx = ~indx;
                                    //   Case 3, Linear Interpolation between TLoad[i - 1] (x1, y1), and TLoad[i] (x2, y2)
                                    double y1, y2;
                                    DateTime x1, x2;
                                    y1 = list.Values[indx - 1]; x1 = list.Keys[indx - 1];
                                    y2 = list.Values[indx]; x2 = list.Keys[indx];
                                    double m = (y2 - y1) / (x2 - x1).TotalDays;
                                    RetLoad = m * (TIHolder - x1).TotalDays + y1;
                                }

                            }


                        }

                    }
                }
            }
            return RetLoad;
        }

    } // end TLoadings


    [Serializable]
    [KnownType(typeof(LoadingsRecord))]
    [DataContract]
    public class LoadingsRecord
    {
        [DataMember] public TLoadings Loadings = new TLoadings();
        // Alt_Loadings is reserved for point source, non pont source and
        // direct precipitation loadings; these vars are relevant only for
        // nstate in [H2OTox,MeHg,HgII,Hg0,Phosphate,Ammonia,Nitrate,All SuspDetr]
        // Has_Alt_Loadings(nstate) is a boolean function in GLOBALS
        [DataMember] public TLoadings[] Alt_Loadings = new TLoadings[3];   // Time series loading

        public LoadingsRecord()
            {
            }

        
        // -------------------------------------------------------------------
        public double ReturnLoad(DateTime TimeIndex)
        {
            double RetLoad;
            // Hold Result
            if (Loadings.UseConstant)
            {
                RetLoad = Loadings.ConstLoad;
            }
            else
            {
                RetLoad = 0;
                if (Loadings != null)
                {
                    RetLoad = Loadings.ReturnLoad(TimeIndex);
                }
            }
            // else
            RetLoad = RetLoad * Loadings.MultLdg;
            return RetLoad;
        }

        // -------------------------------------------------------------------
        public double ReturnAltLoad(DateTime TimeIndex, int AltLdg)
        {
            double RetLoad;
            // Hold Result
            if (Alt_Loadings[AltLdg].UseConstant)
            {
                RetLoad = Alt_Loadings[AltLdg].ConstLoad;
            }
            else
            {
                RetLoad = 0;
                if (Alt_Loadings[AltLdg] != null)
                {
                    RetLoad = Alt_Loadings[AltLdg].ReturnLoad(TimeIndex);
                }
            }
            // else
            RetLoad = RetLoad * Alt_Loadings[AltLdg].MultLdg;
            return RetLoad;
        }

    } // end Loadings



}



