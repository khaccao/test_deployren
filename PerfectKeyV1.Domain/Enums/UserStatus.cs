using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PerfectKeyV1.Domain.Enums
{
    public enum UserStatus
    {
        Deleted = -1,
        Active = 0,
        Pending = 1
    }

    public enum UserType
    {
        HotelAdmin = 0,
        User = 1,
        SuperAdmin = 2
    }

    public enum ElementType
    {
        POS,
        ROOM,
        TABLE,
        BAR,
        KITCHEN
    }
    public enum SessionStatus
    {
        Active,
        Inactive,
        Expired
    }
}
