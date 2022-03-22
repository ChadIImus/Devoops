﻿using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Minitwit.Models.Context;
using Minitwit.Models.Entity;
using Minitwit.Repositories;
using Minitwit.Test.Context;
using System;
using Xunit;

namespace Minitwit.Test.Repositories
{
    public class LatestRepositoryTest : IDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly MinitwitContext _context;
        private readonly LatestRepository _repository;

        public LatestRepositoryTest()
        {
            _connection = new SqliteConnection("DataSource=:memory:");
            _connection.Open();
            var builder = new DbContextOptionsBuilder<MinitwitContext>().UseSqlite(_connection);
            _context = new MinitwitTestContext(builder.Options);
            _context.Database.EnsureCreated();
            _repository = new LatestRepository(_context);
        }

        [Fact]
        public void DummyTest()
        {

            Assert.Equal(true,true);
        }

        public void Dispose()
        {
            _context.Dispose();
            _connection.Dispose();
        }
    }
}
