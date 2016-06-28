﻿using System;
using System.Data.SqlClient;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Xunit;
using YesSql.Core.Services;
using YesSql.Storage.Cache;
using YesSql.Storage.InMemory;
using YesSql.Storage.LightningDB;
using YesSql.Storage.Sql;
using YesSql.Tests.Indexes;
using YesSql.Tests.Models;

#if NET451
using Microsoft.SqlServer.Types;
#endif

namespace YesSql.Tests
{
    /// <summary>
    /// Run all tests with a SqlServer document storage
    /// </summary>
    public abstract class SqliteTests : CoreTests
    {
        private TemporaryFolder _tempFolder;

        public SqliteTests()
        {
            _store = new Store(cfg =>
            {
                _tempFolder = new TemporaryFolder();
                cfg.ConnectionFactory = new DbConnectionFactory<SqliteConnection>(@"Data Source=" + _tempFolder.Folder + "yessql.db;Cache=Shared", true);
                cfg.IsolationLevel = System.Data.IsolationLevel.Serializable;

                // Sqlite needs two databases and two connection factories as transactions can't be nested
                // and using a single connection would create a new transaction when creating documents.
                // The other solution is to create a custom document factory that doesn't create new transactions
                var storageConnectionFactory = new DbConnectionFactory<SqliteConnection>(@"Data Source=" + _tempFolder.Folder + "yessql_content.db;Cache=Shared", true);
                cfg.DocumentStorageFactory = new SqlDocumentStorageFactory(storageConnectionFactory, cfg.IsolationLevel);
            });

            CleanDatabase();
            CreateTables();
        }

        protected override void OnCleanDatabase(ISession session)
        {
            base.OnCleanDatabase(session);

            session.ExecuteMigration(schemaBuilder => schemaBuilder
                .DropTable("Content"), false
            );
        }
    }
    public class SqlServerTests : CoreTests
    {
        public SqlServerTests()
        {
            _store = new Store(cfg =>
            {
                cfg.ConnectionFactory = new DbConnectionFactory<SqlConnection>(ConnectionString);
                cfg.IsolationLevel = System.Data.IsolationLevel.ReadUncommitted;
                cfg.DocumentStorageFactory = new SqlDocumentStorageFactory(cfg.ConnectionFactory);
            });

            CleanDatabase();
            CreateTables();
        }

        protected override void OnCleanDatabase(ISession session)
        {
            base.OnCleanDatabase(session);

            session.ExecuteMigration(schemaBuilder => schemaBuilder
                .DropTable("Content"), false
            );
        }
    }
    public abstract class InMemoryTests : CoreTests
    {
        public InMemoryTests()
        {
            _store = new Store(cfg =>
            {
                cfg.ConnectionFactory = new DbConnectionFactory<SqlConnection>(ConnectionString);
                cfg.IsolationLevel = System.Data.IsolationLevel.ReadUncommitted;
                cfg.DocumentStorageFactory = new InMemoryDocumentStorageFactory();
            });

            CleanDatabase();
            CreateTables();
        }

        protected override void OnCleanDatabase(ISession session)
        {
            base.OnCleanDatabase(session);
        }

        protected override void OnDispose()
        {
            base.OnDispose();
        }
    }
    public abstract class CacheTests : CoreTests
    {
        public CacheTests()
        {
            _store = new Store(cfg =>
            {
                cfg.ConnectionFactory = new DbConnectionFactory<SqlConnection>(ConnectionString);
                cfg.IsolationLevel = System.Data.IsolationLevel.ReadUncommitted;
                cfg.DocumentStorageFactory = new CacheDocumentStorageFactory(new SqlDocumentStorageFactory(cfg.ConnectionFactory));
            });

            CleanDatabase();
            CreateTables();
        }

        protected override void OnCleanDatabase(ISession session)
        {
            base.OnCleanDatabase(session);

            session.ExecuteMigration(schemaBuilder => schemaBuilder
                .DropTable("Content"), false
            );
        }
    }
    public abstract class LightningDBTests : CoreTests
    {
        private TemporaryFolder _tempFolder;

        public LightningDBTests()
        {
            _store = new Store(cfg =>
            {
                cfg.ConnectionFactory = new DbConnectionFactory<SqlConnection>(ConnectionString);
                cfg.IsolationLevel = System.Data.IsolationLevel.ReadUncommitted;

                _tempFolder = new TemporaryFolder();
                cfg.DocumentStorageFactory = new LightningDocumentStorageFactory(_tempFolder.Folder);
            });

            CleanDatabase();
            CreateTables();
        }

        protected override void OnCleanDatabase(ISession session)
        {
            base.OnCleanDatabase(session);
        }
    }

    public abstract class CoreTests : IDisposable
    {

        protected IStore _store;

        public static string AppVeyorConnectionString => @"server = (local)\SQL2014;Database=master;User ID = sa; Password=Password12!";
        public static string Server => "."; //@"(localdb)\MSSQLLocalDB";
        public static string LocalConnectionString => $"Data Source={Server};Initial Catalog=tempdb;Integrated Security=True";

        public static string ConnectionString =>
            !String.IsNullOrEmpty(Environment.GetEnvironmentVariable("APPVEYOR"))
                ? AppVeyorConnectionString
                : LocalConnectionString; 
            

        public CoreTests()
        {

        }

        public void Dispose()
        {
            CleanDatabase();
            _store.Dispose();

            OnDispose();
        }

        protected virtual void OnDispose()
        {
        }

        protected virtual void CleanDatabase()
        {
            using (var session = _store.CreateSession())
            {
                // Remove existing tables
                session.ExecuteMigration(schemaBuilder => schemaBuilder
                    .DropReduceIndexTable(nameof(ArticlesByDay)), false
                );

                session.ExecuteMigration(schemaBuilder => schemaBuilder
                    .DropMapIndexTable(nameof(PersonByName)), false
                );

                session.ExecuteMigration(schemaBuilder => schemaBuilder
                    .DropMapIndexTable(nameof(PersonByAge)), false
                );

                session.ExecuteMigration(schemaBuilder => schemaBuilder
                    .DropMapIndexTable(nameof(PublishedArticle)), false
                );

#if NET451
                session.ExecuteMigration(schemaBuilder => schemaBuilder
                    .DropMapIndexTable(nameof(PinPointByLocation)), false
                );
#endif

                session.ExecuteMigration(schemaBuilder => schemaBuilder
                    .DropTable("Document"), false
                );

                session.ExecuteMigration(schemaBuilder => schemaBuilder
                    .DropTable(LinearBlockIdGenerator.TableName), false
                );

                OnCleanDatabase(session);

            }
        }

        protected virtual void OnCleanDatabase(ISession session)
        {

        }

        public void CreateTables()
        {
            // Create tables
            _store.InitializeAsync().Wait();

            using (var session = _store.CreateSession())
            {
                session.ExecuteMigration(schemaBuilder => schemaBuilder
                    .CreateReduceIndexTable(nameof(ArticlesByDay), column => column
                        .Column<int>(nameof(ArticlesByDay.Count))
                        .Column<int>(nameof(ArticlesByDay.DayOfYear))
                    )
                );

                session.ExecuteMigration(schemaBuilder => schemaBuilder
                    .CreateMapIndexTable(nameof(PersonByName), column => column
                        .Column<string>(nameof(PersonByName.Name))
                    )
                );

                session.ExecuteMigration(schemaBuilder => schemaBuilder
                    .CreateMapIndexTable(nameof(PersonByAge), column => column
                        .Column<int>(nameof(PersonByAge.Age))
                    )
                );

                session.ExecuteMigration(schemaBuilder => schemaBuilder
                    .CreateMapIndexTable(nameof(PublishedArticle), column => { }
                    )
                );

#if NET451
                session.ExecuteMigration(builder => builder
                    .CreateMapIndexTable(nameof(PinPointByLocation), table => table
                        .Column<SqlGeography>("Location")
                    )
                );
#endif

            }
        }

        [Fact]
        public void ShouldCreateDatabase()
        {
            using (var session = _store.CreateSession())
            {
                var doc = new Product();
                session.Save(doc);
            }
        }

        [Fact]
        public void ShouldSaveCustomObject()
        {
            using (var session = _store.CreateSession())
            {
                var bill = new Person
                {
                    Firstname = "Bill",
                    Lastname = "Gates"
                };

                session.Save(bill);
            }
        }

        [Fact]
        public void ShouldSaveSeveralObjects()
        {
            using (var session = _store.CreateSession())
            {
                var bill = new Person
                {
                    Firstname = "Bill",
                    Lastname = "Gates"
                };

                var steve = new Person
                {
                    Firstname = "Steve",
                    Lastname = "Balmer"
                };

                session.Save(bill);
                session.Save(steve);
            }
        }

        [Fact]
        public void ShouldSaveAnonymousObject()
        {
            using (var session = _store.CreateSession())
            {
                var bill = new
                {
                    Firstname = "Bill",
                    Lastname = "Gates"
                };

                var steve = new
                {
                    Firstname = "Steve",
                    Lastname = "Balmer"
                };

                session.Save(bill);
                session.Save(steve);
            }
        }

        [Fact]
        public async Task ShouldLoadAnonymousDocument()
        {
            using (var session = _store.CreateSession())
            {
                var bill = new
                {
                    Firstname = "Bill",
                    Lastname = "Gates",
                    Address = new
                    {
                        Street = "1 Microsoft Way",
                        City = "Redmond"
                    }
                };

                session.Save(bill);
            }

            using (var session = _store.CreateSession())
            {
                dynamic person = await session.QueryAsync().Any().FirstOrDefault();

                Assert.NotNull(person);
                Assert.Equal("Bill", (string)person.Firstname);
                Assert.Equal("Gates", (string)person.Lastname);

                Assert.NotNull(person.Address);
                Assert.Equal("1 Microsoft Way", (string)person.Address.Street);
                Assert.Equal("Redmond", (string)person.Address.City);
            }
        }

        [Fact]
        public async Task ShouldSerializeComplexObject()
        {
            int productId;

            using (var session = _store.CreateSession())
            {
                var product = new Product
                {
                    Cost = 3.99m,
                    Name = "Milk",
                };

                session.Save(product);
                await session.CommitAsync();
                productId = product.Id;

                session.Save(new Order
                {
                    Customer = "customers/microsoft",
                    OrderLines =
                        {
                            new OrderLine
                            {
                                ProductId = product.Id,
                                Quantity = 3
                            },
                        }
                });
            }

            using (var session = _store.CreateSession())
            {
                var prod = await session.GetAsync<Product>(productId);
                Assert.NotNull(prod);
                Assert.Equal("Milk", prod.Name);
            }
        }

        [Fact]
        public void ShouldAssignIdWhenSaved()
        {
            using (var session = _store.CreateSession())
            {
                var bill = new Person
                {
                    Firstname = "Bill",
                    Lastname = "Gates"
                };

                Assert.True(bill.Id == 0);
                session.Save(bill);
                Assert.True(bill.Id != 0);
            }
        }

        [Fact]
        public async Task ShouldAutoFlushOnGet()
        {
            using (var session = _store.CreateSession())
            {
                var bill = new Person
                {
                    Firstname = "Bill",
                    Lastname = "Gates"
                };

                session.Save(bill);
                var newBill = await session.GetAsync<Person>(bill.Id);

                Assert.Same(newBill, bill);
            }
        }

        [Fact]
        public async Task ShouldKeepTrackedOnAutoFlush()
        {
            using (var session = _store.CreateSession())
            {
                var bill = new Person
                {
                    Firstname = "Bill",
                    Lastname = "Gates"
                };

                session.Save(bill);
                var newBill = await session.GetAsync<Person>(bill.Id);

                Assert.Same(newBill, bill);
            }
        }

        [Fact]
        public async Task ShouldUpdateAutoflushedIndex()
        {
            // When auto-flush is called on an entity
            // its indexes should be updated on the actual commit

            _store.RegisterIndexes<PersonIndexProvider>();

            using (var session = _store.CreateSession())
            {
                var bill = new Person
                {
                    Firstname = "Bill",
                    Lastname = "Gates"
                };

                session.Save(bill);

                // This query should force the index to be persisted

                Assert.Equal(1, await session.QueryAsync<Person, PersonByName>().Count());

                bill.Firstname = "Bill2";

                Assert.Equal(1, await session.QueryAsync<Person, PersonByName>().Where(x => x.Name == "Bill2").Count());

                bill.Firstname = "Bill3";

                Assert.Equal(1, await session.QueryIndexAsync<PersonByName>().Count());
            }

            using (var session = _store.CreateSession())
            {
                Assert.Equal(1, await session.QueryIndexAsync<PersonByName>().Count());
                Assert.Equal(1, await session.QueryIndexAsync<PersonByName>().Where(x => x.Name == "Bill3").Count());
            }
        }

        [Fact]
        public async Task ShouldCreateIndexAndLinkToDocument()
        {
            _store.RegisterIndexes<PersonIndexProvider>();

            using (var session = _store.CreateSession())
            {
                var bill = new Person
                {
                    Firstname = "Bill",
                    Lastname = "Gates",
                };

                var steve = new Person
                {
                    Firstname = "Steve",
                    Lastname = "Balmer"
                };

                session.Save(bill);
                session.Save(steve);
            }

            using (var session = _store.CreateSession())
            {
                Assert.Equal(2, await session.QueryIndexAsync<PersonByName>().Count());
                Assert.Equal(1, await session.QueryIndexAsync<PersonByName>(x => x.Name == "Bill").Count());
                Assert.Equal(1, await session.QueryIndexAsync<PersonByName>(x => x.Name == "Steve").Count());
                Assert.Equal(0, await session.QueryIndexAsync<PersonByName>(x => x.Name == "Joe").Count());

                var person = await session
                    .QueryAsync<Person, PersonByName>()
                    .Where(x => x.Name == "Bill")
                    .FirstOrDefault();

                Assert.NotNull(person);
                Assert.Equal("Bill", person.Firstname);
            }
        }

        [Fact]
        public async Task ShouldJoinMapIndexes()
        {
            _store.RegisterIndexes<PersonIndexProvider>();
            _store.RegisterIndexes<PersonAgeIndexProvider>();

            using (var session = _store.CreateSession())
            {
                var bill = new Person
                {
                    Firstname = "Bill",
                    Age = 1
                };

                var steve = new Person
                {
                    Firstname = "Steve",
                    Age = 2
                };

                var paul = new Person
                {
                    Firstname = "Paul",
                    Age = 2
                };

                session.Save(bill);
                session.Save(steve);
                session.Save(paul);
            }

            using (var session = _store.CreateSession())
            {
                Assert.Equal(3, await session.QueryIndexAsync<PersonByName>().Count());
                Assert.Equal(2, await session.QueryIndexAsync<PersonByAge>(x => x.Age == 2).Count());
                Assert.Equal(1, await session.QueryAsync().For<Person>()
                    .With<PersonByName>(x => x.Name == "Steve")
                    .With<PersonByAge>(x => x.Age == 2)
                    .Count());
            }
        }

        [Fact]
        public async Task ShouldReturnImplicitlyFilteredType()
        {
            _store.RegisterIndexes<PersonIndexProvider>();

            using (var session = _store.CreateSession())
            {
                session.Save(new Article());
                session.Save(new Person { Firstname = "Bill" });
                session.Save(new Person { Firstname = "Steve" });
                session.Save(new Person { Firstname = "Paul" });
            }

            using (var session = _store.CreateSession())
            {
                Assert.Equal(3, await session.QueryIndexAsync<PersonByName>().Count());
                Assert.Equal(3, await session.QueryAsync().For<Person>().Count());
                Assert.Equal(3, await session.QueryAsync().For<Person>(false).With<PersonByName>().Count());
                Assert.Equal(4, await session.QueryAsync().For<Person>(false).Count());
            }
        }

        [Fact]
        public async Task ShouldOrderJoinedMapIndexes()
        {
            _store.RegisterIndexes<PersonIndexProvider>();
            _store.RegisterIndexes<PersonAgeIndexProvider>();

            using (var session = _store.CreateSession())
            {
                var bill = new Person
                {
                    Firstname = "Bill",
                    Age = 1
                };

                var steve = new Person
                {
                    Firstname = "Steve",
                    Age = 2
                };

                var paul = new Person
                {
                    Firstname = "Scott",
                    Age = 2
                };

                session.Save(bill);
                session.Save(steve);
                session.Save(paul);
            }

            using (var session = _store.CreateSession())
            {
                Assert.Equal(2, await session.QueryAsync().For<Person>()
                    .With<PersonByName>(x => x.Name.StartsWith("S"))
                    .With<PersonByAge>(x => x.Age == 2)
                    .Count());

                Assert.Equal("Scott", (await session.QueryAsync().For<Person>()
                    .With<PersonByName>(x => x.Name.StartsWith("S"))
                    .OrderBy(x => x.Name)
                    .With<PersonByAge>(x => x.Age == 2)
                    .FirstOrDefault())
                    .Firstname);

                Assert.Equal("Steve", (await session.QueryAsync().For<Person>()
                    .With<PersonByName>(x => x.Name.StartsWith("S"))
                    .OrderByDescending(x => x.Name)
                    .With<PersonByAge>(x => x.Age == 2)
                    .FirstOrDefault())
                    .Firstname);
            }
        }

        [Fact]
        public async Task ShouldJoinReduceIndex()
        {
            _store.RegisterIndexes<ArticleIndexProvider>();
            _store.RegisterIndexes<PublishedArticleIndexProvider>();

            using (var session = _store.CreateSession())
            {
                var dates = new[]
                {
                    new DateTime(2011, 11, 1), // published
                    new DateTime(2011, 11, 2), // not published
                    new DateTime(2011, 11, 3), // published
                    new DateTime(2011, 11, 4), // not published
                    new DateTime(2011, 11, 1), // published
                    new DateTime(2011, 11, 2), // not published
                    new DateTime(2011, 11, 3), // published
                    new DateTime(2011, 11, 1), // not published
                    new DateTime(2011, 11, 2), // published
                    new DateTime(2011, 11, 1) // not published
                };

                var articles = dates.Select((x, i) => new Article
                {
                    IsPublished = i % 2 == 0, // half are published
                    PublishedUtc = x
                });

                foreach (var article in articles)
                {
                    session.Save(article);
                }

            }

            using (var session = _store.CreateSession())
            {
                Assert.Equal(4, await session.QueryIndexAsync<ArticlesByDay>().Count());

                Assert.Equal(4, await session.QueryAsync().For<Article>().With<ArticlesByDay>(x => x.DayOfYear == new DateTime(2011, 11, 1).DayOfYear).Count());
                Assert.Equal(3, await session.QueryAsync().For<Article>().With<ArticlesByDay>(x => x.DayOfYear == new DateTime(2011, 11, 2).DayOfYear).Count());
                Assert.Equal(2, await session.QueryAsync().For<Article>().With<ArticlesByDay>(x => x.DayOfYear == new DateTime(2011, 11, 3).DayOfYear).Count());
                Assert.Equal(1, await session.QueryAsync().For<Article>().With<ArticlesByDay>(x => x.DayOfYear == new DateTime(2011, 11, 4).DayOfYear).Count());

                Assert.Equal(2, await session.QueryAsync().For<Article>().With<PublishedArticle>().With<ArticlesByDay>(x => x.DayOfYear == new DateTime(2011, 11, 1).DayOfYear).Count());
                Assert.Equal(1, await session.QueryAsync().For<Article>().With<PublishedArticle>().With<ArticlesByDay>(x => x.DayOfYear == new DateTime(2011, 11, 2).DayOfYear).Count());
                Assert.Equal(2, await session.QueryAsync().For<Article>().With<PublishedArticle>().With<ArticlesByDay>(x => x.DayOfYear == new DateTime(2011, 11, 3).DayOfYear).Count());
                Assert.Equal(0, await session.QueryAsync().For<Article>().With<PublishedArticle>().With<ArticlesByDay>(x => x.DayOfYear == new DateTime(2011, 11, 4).DayOfYear).Count());
            }
        }

        [Fact]
        public async Task LoadingDocumentShouldNotDuplicateIndex()
        {
            _store.RegisterIndexes<PersonIndexProvider>();

            using (var session = _store.CreateSession())
            {
                var bill = new Person
                {
                    Firstname = "Bill",
                    Lastname = "Gates",
                };

                session.Save(bill);
            }

            using (var session = _store.CreateSession())
            {
                Assert.Equal(1, await session.QueryIndexAsync<PersonByName>().Count());
                Assert.Equal(1, await session.QueryIndexAsync<PersonByName>(x => x.Name == "Bill").Count());

                var person = await session
                    .QueryAsync<Person, PersonByName>()
                    .Where(x => x.Name == "Bill")
                    .FirstOrDefault();

                Assert.NotNull(person);
                Assert.Equal("Bill", person.Firstname);
            }

            using (var session = _store.CreateSession())
            {
                Assert.Equal(1, await session.QueryIndexAsync<PersonByName>().Count());
                Assert.Equal(1, await session.QueryIndexAsync<PersonByName>(x => x.Name == "Bill").Count());

                var person = await session
                    .QueryAsync<Person, PersonByName>()
                    .Where(x => x.Name == "Bill")
                    .FirstOrDefault();

                Assert.NotNull(person);
                Assert.Equal("Bill", person.Firstname);
            }
        }

        [Fact]
        public async Task ShouldReduce()
        {
            _store.RegisterIndexes<ArticleIndexProvider>();

            using (var session = _store.CreateSession())
            {
                var dates = new[]
                {
                    new DateTime(2011, 11, 1),
                    new DateTime(2011, 11, 2),
                    new DateTime(2011, 11, 3),
                    new DateTime(2011, 11, 4),
                    new DateTime(2011, 11, 1),
                    new DateTime(2011, 11, 2),
                    new DateTime(2011, 11, 3),
                    new DateTime(2011, 11, 1),
                    new DateTime(2011, 11, 2),
                    new DateTime(2011, 11, 1)
                };

                var articles = dates.Select(x => new Article
                {
                    PublishedUtc = x
                });

                foreach (var article in articles)
                {
                    session.Save(article);
                }

            }

            using (var session = _store.CreateSession())
            {
                Assert.Equal(4, await session.QueryIndexAsync<ArticlesByDay>().Count());

                Assert.Equal(1, await session.QueryIndexAsync<ArticlesByDay>(x => x.DayOfYear == new DateTime(2011, 11, 1).DayOfYear).Count());
                Assert.Equal(1, await session.QueryIndexAsync<ArticlesByDay>(x => x.DayOfYear == new DateTime(2011, 11, 2).DayOfYear).Count());
                Assert.Equal(1, await session.QueryIndexAsync<ArticlesByDay>(x => x.DayOfYear == new DateTime(2011, 11, 3).DayOfYear).Count());
                Assert.Equal(1, await session.QueryIndexAsync<ArticlesByDay>(x => x.DayOfYear == new DateTime(2011, 11, 4).DayOfYear).Count());

                Assert.Equal(4, await session.QueryAsync<Article, ArticlesByDay>(x => x.DayOfYear == new DateTime(2011, 11, 1).DayOfYear).Count());
                Assert.Equal(3, await session.QueryAsync<Article, ArticlesByDay>(x => x.DayOfYear == new DateTime(2011, 11, 2).DayOfYear).Count());
                Assert.Equal(2, await session.QueryAsync<Article, ArticlesByDay>(x => x.DayOfYear == new DateTime(2011, 11, 3).DayOfYear).Count());
                Assert.Equal(1, await session.QueryAsync<Article, ArticlesByDay>(x => x.DayOfYear == new DateTime(2011, 11, 4).DayOfYear).Count());
            }
        }

        [Fact]
        public async Task ShouldReduceAndMergeWithDatabase()
        {
            _store.RegisterIndexes<ArticleIndexProvider>();

            using (var session = _store.CreateSession())
            {
                var dates = new[]
                {
                    new DateTime(2011, 11, 1),
                    new DateTime(2011, 11, 2),
                    new DateTime(2011, 11, 3),
                    new DateTime(2011, 11, 4),
                    new DateTime(2011, 11, 1),
                    new DateTime(2011, 11, 2),
                    new DateTime(2011, 11, 3),
                    new DateTime(2011, 11, 1),
                    new DateTime(2011, 11, 2),
                    new DateTime(2011, 11, 1)
                };

                var articles = dates.Select(x => new Article
                {
                    PublishedUtc = x
                });

                foreach (var article in articles)
                {
                    session.Save(article);
                }
            }

            using (var session = _store.CreateSession())
            {
                session.Save(new Article { PublishedUtc = new DateTime(2011, 11, 1) });
            }

            using (var session = _store.CreateSession())
            {
                Assert.Equal(4, await session.QueryIndexAsync<ArticlesByDay>().Count());

                Assert.Equal(1, await session.QueryIndexAsync<ArticlesByDay>(x => x.DayOfYear == new DateTime(2011, 11, 1).DayOfYear).Count());
                Assert.Equal(1, await session.QueryIndexAsync<ArticlesByDay>(x => x.DayOfYear == new DateTime(2011, 11, 2).DayOfYear).Count());
                Assert.Equal(1, await session.QueryIndexAsync<ArticlesByDay>(x => x.DayOfYear == new DateTime(2011, 11, 3).DayOfYear).Count());
                Assert.Equal(1, await session.QueryIndexAsync<ArticlesByDay>(x => x.DayOfYear == new DateTime(2011, 11, 4).DayOfYear).Count());

                Assert.Equal(5, await session.QueryAsync<Article, ArticlesByDay>(x => x.DayOfYear == new DateTime(2011, 11, 1).DayOfYear).Count());
                Assert.Equal(3, await session.QueryAsync<Article, ArticlesByDay>(x => x.DayOfYear == new DateTime(2011, 11, 2).DayOfYear).Count());
                Assert.Equal(2, await session.QueryAsync<Article, ArticlesByDay>(x => x.DayOfYear == new DateTime(2011, 11, 3).DayOfYear).Count());
                Assert.Equal(1, await session.QueryAsync<Article, ArticlesByDay>(x => x.DayOfYear == new DateTime(2011, 11, 4).DayOfYear).Count());
            }
        }

        [Fact]
        public async Task MultipleIndexesShoudNotConflict()
        {
            _store.RegisterIndexes<ArticleIndexProvider>();
            _store.RegisterIndexes<PersonIndexProvider>();

            using (var session = _store.CreateSession())
            {
                session.Save(new Article
                {
                    PublishedUtc = new DateTime(2011, 11, 1)
                });
            }

            using (var session = _store.CreateSession())
            {
                session.Save(new Article
                {
                    PublishedUtc = new DateTime(2011, 11, 1)
                });
            }

            using (var session = _store.CreateSession())
            {
                Assert.Equal(1, await session.QueryIndexAsync<ArticlesByDay>().Count());
            }
        }

        [Fact]
        public async Task ShouldDeleteCustomObject()
        {
            _store.RegisterIndexes<PersonIndexProvider>();

            var bill = new Person
            {
                Firstname = "Bill",
                Lastname = "Gates"
            };

            using (var session = _store.CreateSession())
            {
                session.Save(bill);
            }

            using (var session = _store.CreateSession())
            {
                var person = await session.QueryAsync().For<Person>().FirstOrDefault();
                Assert.NotNull(person);

                session.Delete(person);
            }

            using (var session = _store.CreateSession())
            {
                var person = await session.QueryAsync().For<Person>().FirstOrDefault();
                Assert.Null(person);
            }
        }

        [Fact]
        public async Task RemovingDocumentShouldDeleteMappedIndex()
        {
            _store.RegisterIndexes<PersonIndexProvider>();

            var bill = new Person
            {
                Firstname = "Bill",
                Lastname = "Gates"
            };

            using (var session = _store.CreateSession())
            {
                session.Save(bill);
            }

            using (var session = _store.CreateSession())
            {
                var personByName = await session.QueryIndexAsync<PersonByName>().FirstOrDefault();
                Assert.NotNull(personByName);

                var person = await session.QueryAsync().For<Person>().FirstOrDefault();
                Assert.NotNull(person);

                session.Delete(person);
            }

            using (var session = _store.CreateSession())
            {
                var personByName = await session.QueryIndexAsync<PersonByName>().FirstOrDefault();
                Assert.Null(personByName);
            }
        }

        [Fact]
        public async Task RemovingDocumentShouldDeleteReducedIndex()
        {
            _store.RegisterIndexes<ArticleIndexProvider>();

            using (var session = _store.CreateSession())
            {
                var dates = new[]
                {
                    new DateTime(2011, 11, 1),
                    new DateTime(2011, 11, 2),
                    new DateTime(2011, 11, 3),
                    new DateTime(2011, 11, 4),
                    new DateTime(2011, 11, 1),
                    new DateTime(2011, 11, 2),
                    new DateTime(2011, 11, 3),
                    new DateTime(2011, 11, 1),
                    new DateTime(2011, 11, 2),
                    new DateTime(2011, 11, 1)
                };

                var articles = dates.Select(x => new Article
                {
                    PublishedUtc = x
                });

                foreach (var article in articles)
                {
                    session.Save(article);
                }

            }

            using (var session = _store.CreateSession())
            {
                Assert.Equal(10, await session.QueryAsync().For<Article>().Count());
                Assert.Equal(4, await session.QueryIndexAsync<ArticlesByDay>().Count());
            }

            // delete a document
            using (var session = _store.CreateSession())
            {
                var article = await session.QueryAsync<Article, ArticlesByDay>().Where(b => b.DayOfYear == new DateTime(2011, 11, 4).DayOfYear).FirstOrDefault();
                Assert.NotNull(article);
                session.Delete(article);
            }

            // there should be only 3 indexes left
            using (var session = _store.CreateSession())
            {
                // document was deleted
                Assert.Equal(9, await session.QueryAsync().For<Article>().Count());
                // index was deleted
                Assert.Equal(3, await session.QueryIndexAsync<ArticlesByDay>().Count());
            }
        }

        [Fact]
        public async Task UpdatingDocumentShouldUpdateReducedIndex()
        {
            _store.RegisterIndexes<ArticleIndexProvider>();

            using (var session = _store.CreateSession())
            {
                var dates = new[]
                {
                    new DateTime(2011, 11, 1),
                    new DateTime(2011, 11, 1),
                    new DateTime(2011, 11, 1),
                    new DateTime(2011, 11, 1),

                    new DateTime(2011, 11, 2),
                    new DateTime(2011, 11, 2),
                    new DateTime(2011, 11, 2),

                    new DateTime(2011, 11, 3),
                    new DateTime(2011, 11, 3),

                    new DateTime(2011, 11, 4)
                };

                foreach (var date in dates)
                {
                    session.Save(new Article { PublishedUtc = date });
                }

            }

            using (var session = _store.CreateSession())
            {
                // 10 articles
                Assert.Equal(10, await session.QueryAsync().For<Article>().Count());

                // 4 indexes as there are 4 different dates
                Assert.Equal(4, await session.QueryIndexAsync<ArticlesByDay>().Count());
            }

            // change the published date of an article
            using (var session = _store.CreateSession())
            {
                var article = await session
                    .QueryAsync<Article, ArticlesByDay>()
                    .Where(b => b.DayOfYear == new DateTime(2011, 11, 2).DayOfYear)
                    .FirstOrDefault();

                Assert.NotNull(article);

                article.PublishedUtc = new DateTime(2011, 11, 3);

                session.Save(article);

            }

            // there should be the same number of indexes
            using (var session = _store.CreateSession())
            {
                Assert.Equal(10, await session.QueryAsync().For<Article>().Count());
                Assert.Equal(4, await session.QueryIndexAsync<ArticlesByDay>().Count());

                Assert.Equal(4, await session.QueryAsync<Article, ArticlesByDay>(x => x.DayOfYear == new DateTime(2011, 11, 1).DayOfYear).Count());
                Assert.Equal(2, await session.QueryAsync<Article, ArticlesByDay>(x => x.DayOfYear == new DateTime(2011, 11, 2).DayOfYear).Count());
                Assert.Equal(3, await session.QueryAsync<Article, ArticlesByDay>(x => x.DayOfYear == new DateTime(2011, 11, 3).DayOfYear).Count());
                Assert.Equal(1, await session.QueryAsync<Article, ArticlesByDay>(x => x.DayOfYear == new DateTime(2011, 11, 4).DayOfYear).Count());
            }
        }

        [Fact]
        public async Task AlteringDocumentShouldUpdateReducedIndex()
        {
            _store.RegisterIndexes<ArticleIndexProvider>();

            using (var session = _store.CreateSession())
            {
                var dates = new[]
                {
                    new DateTime(2011, 11, 1),
                    new DateTime(2011, 11, 2),
                    new DateTime(2011, 11, 2),
                };

                var articles = dates.Select(x => new Article
                {
                    PublishedUtc = x
                });

                foreach (var article in articles)
                {
                    session.Save(article);
                }

            }

            using (var session = _store.CreateSession())
            {
                // There should be 3 articles
                Assert.Equal(3, await session.QueryAsync().For<Article>().Count());

                // There should be 2 groups
                Assert.Equal(2, await session.QueryIndexAsync<ArticlesByDay>().Count());
            }

            // Deleting a document which was the only one in the reduced group
            using (var session = _store.CreateSession())
            {
                var article = await session.QueryAsync<Article, ArticlesByDay>()
                    .Where(b => b.DayOfYear == new DateTime(2011, 11, 1).DayOfYear)
                    .FirstOrDefault();

                Assert.NotNull(article);
                session.Delete(article);
            }

            // Ensure the document and its index have been deleted
            using (var session = _store.CreateSession())
            {
                // There should be 1 article
                Assert.Equal(2, await session.QueryAsync<Article>().Count());

                // There should be 1 group
                Assert.Equal(1, await session.QueryIndexAsync<ArticlesByDay>().Count());
            }
        }

        [Fact]
        public async Task IndexHasLinkToDocuments()
        {
            _store.RegisterIndexes<ArticleIndexProvider>();

            using (var session = _store.CreateSession())
            {
                var d1 = new Article { PublishedUtc = new DateTime(2011, 11, 1) };
                var d2 = new Article { PublishedUtc = new DateTime(2011, 11, 1) };

                session.Save(d1);
                session.Save(d2);
            }

            using (var session = _store.CreateSession())
            {
                var articles = session.QueryAsync().For<Article>();
                Assert.Equal(2, await articles.Count());
            }
        }

        [Fact]
        public async Task ChangesAreAutoFlushed()
        {
            _store.RegisterIndexes<ArticleIndexProvider>();

            using (var session = _store.CreateSession())
            {
                var d1 = new Article { PublishedUtc = new DateTime(2011, 11, 1) };
                var d2 = new Article { PublishedUtc = new DateTime(2011, 11, 1) };

                session.Save(d1);
                session.Save(d2);

                var articles = session.QueryAsync<Article, ArticlesByDay>(x => x.DayOfYear == 305);
                Assert.Equal(2, await articles.Count());
            }
        }

        [Fact]
        public async Task ShouldOrderOnValueType()
        {
            _store.RegisterIndexes<PersonAgeIndexProvider>();

            using (var session = _store.CreateSession())
            {
                for (int i = 0; i < 100; i++)
                {
                    var person = new Person
                    {
                        Firstname = "Bill" + i,
                        Lastname = "Gates" + i,
                        Age = i
                    };

                    session.Save(person);
                }

            }

            using (var session = _store.CreateSession())
            {
                Assert.Equal(100, await session.QueryIndexAsync<PersonByAge>().Count());
                Assert.Equal(0, (await session.QueryIndexAsync<PersonByAge>().OrderBy(x => x.Age).FirstOrDefault()).Age);
                Assert.Equal(99, (await session.QueryIndexAsync<PersonByAge>().OrderByDescending(x => x.Age).FirstOrDefault()).Age);
            }
        }

        [Fact]
        public async Task CanCountThenListOrdered()
        {
            _store.RegisterIndexes<PersonAgeIndexProvider>();

            using (var session = _store.CreateSession())
            {
                for (int i = 0; i < 100; i++)
                {
                    var person = new Person
                    {
                        Firstname = "Bill" + i,
                        Lastname = "Gates" + i,
                        Age = i
                    };

                    session.Save(person);
                }

            }

            using (var session = _store.CreateSession())
            {
                var query = session.QueryIndexAsync<PersonByAge>().OrderBy(x => x.Age);

                Assert.Equal(100, await query.Count());
                Assert.Equal(100, (await query.List()).Count());
            }
        }

        [Fact]
        public async Task ShouldPageResults()
        {
            _store.RegisterIndexes<PersonIndexProvider>();

            using (var session = _store.CreateSession())
            {
                for (int i = 0; i < 100; i++)
                {
                    var person = new Person
                    {
                        Firstname = "Bill" + i,
                        Lastname = "Gates" + i,
                    };

                    session.Save(person);
                }

            }

            using (var session = _store.CreateSession())
            {
                Assert.Equal(100, await session.QueryIndexAsync<PersonByName>().Count());
                Assert.Equal(10, (await session.QueryIndexAsync<PersonByName>().OrderBy(x => x.Name).Skip(0).Take(10).List()).Count());
                Assert.Equal(1, await session.QueryIndexAsync<PersonByName>(x => x.Name == "Bill0").Count());

                var persons = await session.QueryAsync<Person, PersonByName>().Take(10).List();

                Assert.Equal(10, persons.Count());
            }
        }

        [Fact]
        public async Task ShouldQueryByMappedIndex()
        {
            _store.RegisterIndexes<PersonIndexProvider>();

            using (var session = _store.CreateSession())
            {
                var bill = new Person
                {
                    Firstname = "Bill",
                    Lastname = "Gates",
                };

                var steve = new Person
                {
                    Firstname = "Steve",
                    Lastname = "Balmer"
                };

                session.Save(bill);
                session.Save(steve);
            }

            using (var session = _store.CreateSession())
            {
                Assert.Equal(2, await session.QueryAsync().For<Person>().With<PersonByName>().Count());
                Assert.Equal(1, await session.QueryAsync().For<Person>().With<PersonByName>(x => x.Name == "Steve").Count());
                Assert.Equal(1, await session.QueryAsync().For<Person>().With<PersonByName>().Where(x => x.Name == "Steve").Count());
            }
        }

        [Fact]
        public async Task ShouldQueryByReducedIndex()
        {
            _store.RegisterIndexes<ArticleIndexProvider>();

            using (var session = _store.CreateSession())
            {
                var dates = new[]
                {
                    new DateTime(2011, 11, 1),
                    new DateTime(2011, 11, 2),
                    new DateTime(2011, 11, 3),
                    new DateTime(2011, 11, 4),
                    new DateTime(2011, 11, 1),
                    new DateTime(2011, 11, 2),
                    new DateTime(2011, 11, 3),
                    new DateTime(2011, 11, 1),
                    new DateTime(2011, 11, 2),
                    new DateTime(2011, 11, 1)
                };

                var articles = dates.Select(x => new Article
                {
                    PublishedUtc = x
                });

                foreach (var article in articles)
                {
                    session.Save(article);
                }

            }

            using (var session = _store.CreateSession())
            {
                Assert.Equal(10, await session.QueryAsync().For<Article>().With<ArticlesByDay>().Count());

                Assert.Equal(4, await session.QueryAsync<Article, ArticlesByDay>(x => x.DayOfYear == 305).Count());
                Assert.Equal(3, await session.QueryAsync<Article, ArticlesByDay>(x => x.DayOfYear == 306).Count());
                Assert.Equal(2, await session.QueryAsync<Article, ArticlesByDay>(x => x.DayOfYear == 307).Count());
                Assert.Equal(1, await session.QueryAsync<Article, ArticlesByDay>(x => x.DayOfYear == 308).Count());

                Assert.Equal(7, await session.QueryAsync<Article, ArticlesByDay>(x => x.DayOfYear == 305 || x.DayOfYear == 306).Count());
                Assert.Equal(7, (await session.QueryAsync<Article, ArticlesByDay>(x => x.DayOfYear == 305 || x.DayOfYear == 306).List()).Count());

            }
        }

        [Fact]
        public void ShouldSaveBigDocuments()
        {
            using (var session = _store.CreateSession())
            {
                var bill = new Person
                {
                    Firstname = new String('x', 10000),
                };


                session.Save(bill);
            }
        }

        [Fact]
        public async Task ShouldSavePolymorphicProperties()
        {
            using (var session = _store.CreateSession())
            {
                var drawing = new Drawing
                {
                    Shapes = new Shape[]
                    {
                        new Square { Size = 10 },
                        new Square { Size = 20 },
                        new Circle { Radius = 5 }
                    }
                };

                session.Save(drawing);
            }

            using (var session = _store.CreateSession())
            {
                var drawing = await session.QueryAsync().For<Drawing>().FirstOrDefault();

                Assert.NotNull(drawing);
                Assert.Equal(3, drawing.Shapes.Count);
                Assert.Equal(typeof(Square), drawing.Shapes[0].GetType());
                Assert.Equal(typeof(Square), drawing.Shapes[1].GetType());
                Assert.Equal(typeof(Circle), drawing.Shapes[2].GetType());
            }
        }

        [Fact]
        public async Task ShouldIgnoreNonSerializedAttribute()
        {
            using (var session = _store.CreateSession())
            {

                var dog = new Animal
                {
                    Name = "Doggy",
                    Color = "Pink"
                };

                session.Save(dog);
            }

            using (var session = _store.CreateSession())
            {
                var dog = await session.QueryAsync().For<Animal>().FirstOrDefault();

                Assert.NotNull(dog);
                Assert.Equal("Doggy", dog.Name);
                Assert.Equal(null, dog.Color);
            }
        }


        [Fact]
        public async Task ShouldGetTypeById()
        {
            int circleId;

            using (var session = _store.CreateSession())
            {
                var circle = new Circle
                {
                    Radius = 10
                };

                session.Save(circle);
                await session.CommitAsync();

                circleId = circle.Id;
            }

            using (var session = _store.CreateSession())
            {
                var circle = await session.GetAsync<Circle>(circleId);

                Assert.NotNull(circle);
                Assert.Equal(10, circle.Radius);
            }
        }

        [Fact]
        public async Task ShouldGetDocumentById()
        {
            int circleId;

            using (var session = _store.CreateSession())
            {
                var circle = new Circle
                {
                    Radius = 10
                };

                session.Save(circle);
                await session.CommitAsync();

                circleId = circle.Id;
            }

            using (var session = _store.CreateSession())
            {
                var circle = await session.GetAsync<Circle>(circleId);

                Assert.NotNull(circle);
            }
        }

        [Fact]
        public async Task ShouldGetObjectById()
        {
            int circleId;

            using (var session = _store.CreateSession())
            {
                var circle = new Circle
                {
                    Radius = 10
                };

                session.Save(circle);
                await session.CommitAsync();

                circleId = circle.Id;
            }

            using (var session = _store.CreateSession())
            {
                var circle = await session.GetAsync<Circle>(circleId);

                Assert.NotNull(circle);
                Assert.Equal(typeof(Circle), circle.GetType());
            }
        }

        [Fact]
        public async Task ShouldGetDynamicById()
        {
            int circleId;

            using (var session = _store.CreateSession())
            {
                var circle = new Circle
                {
                    Radius = 10
                };

                session.Save(circle);
                await session.CommitAsync();

                circleId = circle.Id;
            }

            using (var session = _store.CreateSession())
            {
                var circle = await session.GetAsync<dynamic>(circleId);

                Assert.NotNull(circle);
                Assert.Equal(10, (int)circle.Radius);
            }
        }

        [Fact]
        public async Task ShouldAllowMultipleCallToSave()
        {
            using (var session = _store.CreateSession())
            {
                var circle = new Circle
                {
                    Radius = 10
                };

                session.Save(circle);
                session.Save(circle);
                session.Save(circle);
            }

            using (var session = _store.CreateSession())
            {
                var circles = await session.QueryAsync().For<Circle>().List();

                Assert.Equal(1, circles.Count());
            }
        }

        [Fact]
        public async Task ShouldUpdateDisconnectedObject()
        {
            Circle circle;
            using (var session = _store.CreateSession())
            {
                circle = new Circle
                {
                    Radius = 10
                };

                session.Save(circle);
            }

            using (var session = _store.CreateSession())
            {
                circle.Radius = 20;
                session.Save(circle);
            }

            using (var session = _store.CreateSession())
            {
                var circles = await session.QueryAsync().For<Circle>().List();
                Assert.Equal(1, circles.Count());
                Assert.Equal(20, circles.FirstOrDefault().Radius);
            }
        }

        [Fact]
        public async Task ShouldNotCommitTransaction()
        {
            using (var session = _store.CreateSession())
            {
                var circle = new Circle
                {
                    Radius = 10
                };

                session.Save(circle);
                session.Cancel();
            }

            using (var session = _store.CreateSession())
            {
                Assert.Equal(0, await session.QueryAsync().For<Circle>().Count());
            }
        }

        [Fact]
        public async Task ShouldSaveChangesExplicitly()
        {
            using (var session = _store.CreateSession())
            {
                var circle = new Circle
                {
                    Radius = 10
                };

                session.Save(circle);
            }

            using (var session = _store.CreateSession())
            {
                var circle = await session.QueryAsync().For<Circle>().FirstOrDefault();
                Assert.NotNull(circle);

                circle.Radius = 20;
                session.Save(circle);
            }

            using (var session = _store.CreateSession())
            {
                Assert.Equal(20, (await session.QueryAsync().For<Circle>().FirstOrDefault()).Radius);
            }
        }

        [Fact]
        public async Task ShouldNotSaveChangesAutomatically()
        {
            using (var session = _store.CreateSession())
            {
                var circle = new Circle
                {
                    Radius = 10
                };

                session.Save(circle);
            }

            using (var session = _store.CreateSession())
            {
                var circle = await session.QueryAsync().For<Circle>().FirstOrDefault();
                Assert.NotNull(circle);

                circle.Radius = 20;
            }

            using (var session = _store.CreateSession())
            {
                Assert.Equal(10, (await session.QueryAsync().For<Circle>().FirstOrDefault()).Radius);
            }

            using (var session = _store.CreateSession())
            {
                var circle = await session.QueryAsync().For<Circle>().FirstOrDefault();
                Assert.NotNull(circle);

                circle.Radius = 20;
                session.Save(circle);
            }

            using (var session = _store.CreateSession())
            {
                Assert.Equal(20, (await session.QueryAsync().For<Circle>().FirstOrDefault()).Radius);
            }
        }

        [Fact]
        public async Task ShouldMapWithPredicate()
        {
            _store.RegisterIndexes<PublishedArticleIndexProvider>();

            using (var session = _store.CreateSession())
            {
                session.Save(new Article { IsPublished = true });
                session.Save(new Article { IsPublished = true });
                session.Save(new Article { IsPublished = true });
                session.Save(new Article { IsPublished = true });
                session.Save(new Article { IsPublished = false });
                session.Save(new Article { IsPublished = false });
            }

            using (var session = _store.CreateSession())
            {
                Assert.Equal(6, await session.QueryAsync().For<Article>().Count());
                Assert.Equal(4, await session.QueryAsync().For<Article>().With<PublishedArticle>().Count());

                Assert.Equal(4, await session.QueryAsync<Article, PublishedArticle>().Count());
            }
        }

        [Fact]
        public async Task ShouldAcceptEmptyIsIn()
        {
            _store.RegisterIndexes<PersonIndexProvider>();

            using (var session = _store.CreateSession())
            {
                var bill = new Person
                {
                    Firstname = "Bill"
                };

                var steve = new Person
                {
                    Firstname = "Steve"
                };

                var paul = new Person
                {
                    Firstname = "Scott"
                };

                session.Save(bill);
                session.Save(steve);
                session.Save(paul);
            }

            using (var session = _store.CreateSession())
            {
                Assert.Equal(2, await session.QueryAsync().For<Person>()
                    .With<PersonByName>(x => x.Name.IsIn(new[] { "Bill", "Steve" }))
                    .Count());

                Assert.Equal(0, await session.QueryAsync().For<Person>()
                    .With<PersonByName>(x => x.Name.IsIn(new string[0]))
                    .Count());
            }
        }

#if NET451
        [Fact]
        public async Task ShouldStorePinPoints()
        {
            _store.RegisterIndexes<PinPointIndexProvider>();

            using (var session = _store.CreateSession())
            {
                var p1 = new PinPoint { Name = "Amsterdam", Location = SqlGeography.Point(52.372852, 4.893712, 4326) };
                var p2 = new PinPoint { Name = "Seattle", Location = SqlGeography.Point(47.61, -122.33, 4326) };

                session.Save(p1);
                session.Save(p2);
            }
            using (var session = _store.CreateSession())
            {
                Assert.Equal(2, await session.QueryAsync().For<PinPoint>().Count());
            }
        }

        [Fact]
        public async Task ShouldFindPinPointByDistance()
        {
            _store.RegisterIndexes<PinPointIndexProvider>();

            using (var session = _store.CreateSession())
            {
                var p1 = new PinPoint { Name = "Amsterdam", Location = SqlGeography.Point(52.372852, 4.893712, 4326) };
                var p2 = new PinPoint { Name = "Seattle", Location = SqlGeography.Point(47.61, -122.33, 4326) };

                session.Save(p1);
                session.Save(p2);
            }

            using (var session = _store.CreateSession())
            {
                // Distance from Groningen should find Amsterdam within 250km
                var qs = session.QueryAsync<PinPoint, PinPointByLocation>().Where("Location.STDistance('POINT(6.568269 53.219367)') < 250000");

                var ps = await qs.List();

                Assert.Equal(1, ps.Count());
                Assert.Equal("Amsterdam", ps.First().Name);
            }
        }

#endif

    }
}