using YesSql.Core.Indexes;
using YesSql.Tests.Models;

#if NET451
using Microsoft.SqlServer.Types;
#endif

namespace YesSql.Tests.Indexes
{
    public class PinPointByLocation : MapIndex
    {
#if NET451
        public SqlGeography Location { get; set; }
#endif
    }

    public class PinPointIndexProvider : IndexProvider<PinPoint>
    {
        public override void Describe(DescribeContext<PinPoint> context)
        {
#if NET451
            context.For<PinPointByLocation>()
                .Map(point =>
                   new PinPointByLocation { Location = point.Location }
                );
#endif
        }
    }

}
