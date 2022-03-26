﻿using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Minitwit.Models.Context;
using Minitwit.Models.Entity;
using Minitwit.Repositories;
using Minitwit.Test.Context;
using System;
using System.Threading.Tasks;
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
        public async Task Test_get_latest()
        {
            var latest = await _repository.GetLatest();
            Assert.Equal(1101,latest.Value);
        }

        [Fact]
        public async Task Test_get_latest_after_insert()
        {
            await _repository.InsertLatest(new Latest { Id = 2, Value = 1102, CreationTime = System.DateTime.Now });
            var latest = await _repository.GetLatest();
            Assert.Equal(1102, latest.Value);
        }

        public void Dispose()
        {
            _context.Dispose();
            _connection.Dispose();
        }
    }
}
