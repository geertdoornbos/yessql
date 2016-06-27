using System;
using System.Data;

namespace YesSql.Core.Sql.Schema
{
    public class CreateTableCommand : SchemaCommand
    {
        public CreateTableCommand(string name)
            : base(name, SchemaCommandType.CreateTable)
        {
        }

        public CreateTableCommand Column(string columnName, DbType dbType, Action<CreateColumnCommand> column = null)
        {
            var command = new CreateColumnCommand(Name, columnName);
            command.WithType(dbType);

            if (column != null)
            {
                column(command);
            }
            TableCommands.Add(command);
            return this;
        }

        public CreateTableCommand Column<T>(string columnName, Action<CreateColumnCommand> column = null)
        {
            var command = new CreateColumnCommand(Name, columnName);

            Type type = typeof(T);
            switch (type.FullName)
            {
                case "Microsoft.SqlServer.Types.SqlGeography":
                    command.WithUserDefinedType("geography");
                    command.WithType(DbType.Object);
                    break;
                case "Microsoft.SqlServer.Types.SqlGeometry":
                    command.WithUserDefinedType("geometry");
                    command.WithType(DbType.Object);
                    break;
                case "Microsoft.SqlServer.Types.SqlHierarchyId":
                    command.WithUserDefinedType("hierarchyid");
                    command.WithType(DbType.Object);
                    break;
                default:
                    var dbType = SchemaUtils.ToDbType(typeof(T));
                    command.WithType(dbType);
                    break;
            }

            if (column != null)
            {
                column(command);
            }
            TableCommands.Add(command);
            return this;
        }
    }
}
