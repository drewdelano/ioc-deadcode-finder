using System;
using System.Collections.Generic;
using System.Text;

namespace Tests.ProjectWithDeadCode
{
    public class Fun : IFunWithInterfaces
    {
        public void Called()
        {
            throw new NotImplementedException();
        }

        public void NotCalled()
        {
            throw new NotImplementedException();
        }
    }
}
