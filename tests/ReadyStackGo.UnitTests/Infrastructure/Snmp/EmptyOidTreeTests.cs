using FluentAssertions;
using Lextm.SharpSnmpLib;
using ReadyStackGo.Infrastructure.Snmp;

namespace ReadyStackGo.UnitTests.Infrastructure.Snmp;

public class EmptyOidTreeTests
{
    [Fact]
    public void Get_AnyOid_ReturnsNull()
    {
        var tree = new EmptyOidTree();

        tree.Get(new ObjectIdentifier("1.3.6.1.4.1.99999.1.1.1.0")).Should().BeNull();
        tree.Get(new ObjectIdentifier("1.3.6.1.4.1.99999.1.3.1.6.123.456")).Should().BeNull();
        tree.Get(new ObjectIdentifier("0.0")).Should().BeNull();
    }

    [Fact]
    public void GetNext_AnyOid_ReturnsNull()
    {
        var tree = new EmptyOidTree();

        tree.GetNext(new ObjectIdentifier("1.3.6.1.4.1.99999.1")).Should().BeNull();
        tree.GetNext(new ObjectIdentifier("1.3.6.1.4.1.99999.1.4.1.4")).Should().BeNull();
        tree.GetNext(new ObjectIdentifier("0.0")).Should().BeNull();
    }
}
