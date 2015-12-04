﻿using System;
using System.Collections.Generic;
using System.IO;
using FluentAssertions;
using FluentNHibernate.Cfg.Db;
using NHibernate.Cfg;
using NUnit.Framework;
using SharpArch.Domain;
using SharpArch.NHibernate;

namespace Tests.SharpArch.NHibernate
{
    [TestFixture]
    public class NHibernateSessionFactoryBuilderTests
    {
        const string DefaultConfigFile = "sqlite-nhibernate-config.xml";

        [Test]
        public void CanExposeConfiguration()
        {
            var exposeCalled = false;
            Action<Configuration> configure = c => { exposeCalled = true; };

            new NHibernateSessionFactoryBuilder()
                .UseConfigFile(DefaultConfigFile)
                .ExposeConfiguration(configure)
                .BuildConfiguration();

            exposeCalled.Should().BeTrue();
        }

        [Test]
        public void ShouldPersistExposedConfigurationChanges()
        {
            var cache = new InMemoryCache();

            new NHibernateSessionFactoryBuilder()
                .UseConfigFile(DefaultConfigFile)
                .ExposeConfiguration(c=>c.SetProperty("connection.connection_string", "updated-connection"))
                .UseConfigurationCache(cache)
                .BuildConfiguration();

            var config = new NHibernateSessionFactoryBuilder()
                .UseConfigFile(DefaultConfigFile)
                .UseConfigurationCache(cache)
                .BuildConfiguration();

            config.Properties["connection.connection_string"].Should().Be("updated-connection");
        }



        [Test]
        public void CanInitializeWithConfigFile()
        {
            var configuration = new NHibernateSessionFactoryBuilder()
                .UseConfigFile(DefaultConfigFile)
                .BuildConfiguration();

            Assert.That(configuration, Is.Not.Null);

            configuration.BuildSessionFactory();
        }

        [Test]
        public void CanInitializeWithConfigFileAndConfigurationFileCache()
        {
            var configuration = new NHibernateSessionFactoryBuilder()
                .UseConfigurationCache(new NHibernateConfigurationFileCache(new[] {"SharpArch.NHibernate"}))
                .UseConfigFile(DefaultConfigFile)
                .BuildConfiguration();

            Assert.That(configuration, Is.Not.Null);

            configuration.BuildSessionFactory();
        }

        [Test]
        public void CanInitializeWithPersistenceConfigurerAndConfigFile()
        {
            var persistenceConfigurer =
                SQLiteConfiguration.Standard.ConnectionString(c => c.Is("Data Source=:memory:;Version=3;New=True;"));

            var configuration = new NHibernateSessionFactoryBuilder()
                .UsePersistenceConfigurer(persistenceConfigurer)
                .UseConfigFile(DefaultConfigFile)
                .BuildConfiguration();

            Assert.That(configuration, Is.Not.Null);
            configuration.BuildSessionFactory();
        }

        [Test]
        public void CanInitializeWithPersistenceConfigurerAndNoConfigFile()
        {
            var persistenceConfigurer =
                SQLiteConfiguration.Standard.ConnectionString(c => c.Is("Data Source=:memory:;Version=3;New=True;"));

            var configuration = new NHibernateSessionFactoryBuilder()
                .UsePersistenceConfigurer(persistenceConfigurer)
                .BuildConfiguration();

            Assert.That(configuration, Is.Not.Null);
            configuration.BuildSessionFactory();
        }

        [Test]
        public void DoesInitializeFailWhenCachingFileDependencyCannotBeFound()
        {
            Assert.Throws<FileNotFoundException>(
                () =>
                {
                    new NHibernateSessionFactoryBuilder()
                        // Random Guid value as dependency file to cause the exception
                        .UseConfigurationCache(new NHibernateConfigurationFileCache(new[] {Guid.NewGuid().ToString()}))
                        .UseConfigFile(DefaultConfigFile)
                        .BuildConfiguration();
                });
        }

        [Test]
        public void WhenUsingDataAnnotationValidators_ShouldKeepRegisteredPreInsertEventListeners()
        {
            var configuration = new NHibernateSessionFactoryBuilder()
                .UseConfigFile(DefaultConfigFile)
                .UseDataAnnotationValidators(true)
                .BuildConfiguration();

            configuration.EventListeners.PreInsertEventListeners.Should().Contain(l => l is PreInsertListener);
        }

        [Test]
        public void WhenUsingDataAnnotationValidators_ShouldKeepRegisteredPreUpdateEventListeners()
        {
            var configuration = new NHibernateSessionFactoryBuilder()
                .UseConfigFile(DefaultConfigFile)
                .UseDataAnnotationValidators(true)
                .BuildConfiguration();

            configuration.EventListeners.PreUpdateEventListeners.Should().Contain(l => l is PreUpdateListener);
        }
    }


    class InMemoryCache : INHibernateConfigurationCache
    {
        MemoryStream memoryStream;

        public InMemoryCache()
        {
            memoryStream = new MemoryStream();
        }

        public Configuration LoadConfiguration(string configKey, string configPath, IEnumerable<string> mappingAssemblies)
        {
            if (memoryStream.Length == 0)
                return null;

            memoryStream.Position = 0;
            return FileCache.Load<Configuration>(memoryStream);
        }

        public void SaveConfiguration(string configKey, Configuration config)
        {
            memoryStream.SetLength(0);
            FileCache.Save(memoryStream, config);
        }
    }
}