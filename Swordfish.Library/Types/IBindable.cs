using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Swordfish.Library.Types
{
    public interface IBindable
    {
        void Bind();

        void Unbind();
    }
}