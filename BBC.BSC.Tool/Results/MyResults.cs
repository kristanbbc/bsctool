using System;
using System.Collections.Generic;

namespace BBC.BSC.Tool.Results
{
    public class MyResults
    {
        public DateTime Timestamp
        {
            get;
        }
        public List<MyResult> Results = new List<MyResult>();

        public MyResults()
        {
            Timestamp = DateTime.Now;
        }
        public void AddResult(MyResult result)
        {
            Results.Add(result);
        }
    }
}