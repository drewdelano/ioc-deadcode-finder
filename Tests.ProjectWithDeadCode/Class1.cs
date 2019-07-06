using System;

namespace Tests.ProjectWithDeadCode
{
    public class Class1
    {
        public Class1()
        {
        }

        public Class1(string name)
        {
        }

        public void Called()
        {
            var aaa = DateTime.Now;
            var bbb = aaa.AddDays(1);
            var cccc = new Class2();
            cccc.Called();
        }

        public void NotCalled()
        {
        }
    }
}
