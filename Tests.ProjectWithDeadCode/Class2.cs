using System;
using System.Collections.Generic;
using System.Text;

namespace Tests.ProjectWithDeadCode
{
    public class Class2
    {
        public void Called()
        {
            AlsoCalled();
        }

        public void AlsoCalled()
        {
            IFunWithInterfaces fun = new Fun();
            fun.Called();
        }

        public void NotCalled()
        {
        }
    }
}
