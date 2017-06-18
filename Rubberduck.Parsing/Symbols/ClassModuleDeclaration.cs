﻿using Rubberduck.Parsing.Annotations;
using Rubberduck.Parsing.ComReflection;
using Rubberduck.Parsing.VBA;
using Rubberduck.VBEditor;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace Rubberduck.Parsing.Symbols
{
    public sealed class ClassModuleDeclaration : Declaration
    {
        private readonly List<string> _supertypeNames;
        private readonly HashSet<Declaration> _supertypes;
        private readonly ConcurrentDictionary<Declaration, byte> _subtypes;

        private Lazy<bool> _isExtensible;
        private Lazy<bool> _isExposed;
        private Lazy<bool> _hasPredeclaredId;

        public ClassModuleDeclaration(
                  QualifiedMemberName qualifiedName,
                  Declaration projectDeclaration,
                  string name,
                  bool isUserDefined,
                  IEnumerable<IAnnotation> annotations,
                  Attributes attributes,
                  bool hasDefaultInstanceVariable = false,
                  bool isControl = false)
            : base(
                  qualifiedName,
                  projectDeclaration,
                  projectDeclaration,
                  name,
                  null,
                  false,
                  false,
                  Accessibility.Public,
                  DeclarationType.ClassModule,
                  null,
                  Selection.Home,
                  false,
                  null,
                  isUserDefined,
                  annotations,
                  attributes)
        {
            _supertypeNames = new List<string>();
            _supertypes = new HashSet<Declaration>();
            _subtypes = new ConcurrentDictionary<Declaration, byte>();
            IsControl = isControl;
            _isExtensible = new Lazy<bool>(() => IsExtensibleToCache());
            _isExposed = new Lazy<bool>(() => IsExposedToCache());
            if (hasDefaultInstanceVariable)
            {
                _hasPredeclaredId = new Lazy<bool>(() => true);
            }
            else
            {
                _hasPredeclaredId = new Lazy<bool>(() => HasPredeclaredIdToCache());
            }
        }

        // skip IDispatch.. just about everything implements it and RD doesn't need to care about it; don't care about IUnknown either
        private static readonly HashSet<string> IgnoredInterfaces = new HashSet<string>(new[] { "IDispatch", "IUnknown" });

        public ClassModuleDeclaration(ComCoClass coClass, Declaration parent, QualifiedModuleName module,
            Attributes attributes)
            : base(
                module.QualifyMemberName(coClass.Name),
                parent,
                parent,
                coClass.Name,
                null,
                false,
                coClass.EventInterfaces.Any(),
                Accessibility.Public,
                DeclarationType.ClassModule,
                null,
                Selection.Home,
                false,
                null,
                false,
                new List<IAnnotation>(),
                attributes)
        {
            _supertypeNames =
                coClass.ImplementedInterfaces.Where(i => !i.IsRestricted && !IgnoredInterfaces.Contains(i.Name))
                    .Select(i => i.Name)
                    .ToList();
            _supertypes = new HashSet<Declaration>();
            _subtypes = new ConcurrentDictionary<Declaration, byte>();
            IsControl = coClass.IsControl;
            _isExtensible = new Lazy<bool>(() => IsExtensibleToCache());
            _isExposed = new Lazy<bool>(() => IsExposedToCache());
            _hasPredeclaredId = new Lazy<bool>(() => HasPredeclaredIdToCache());
        }

        public ClassModuleDeclaration(ComInterface @interface, Declaration parent, QualifiedModuleName module,
            Attributes attributes)
            : this(
                module.QualifyMemberName(@interface.Name),
                parent,
                @interface.Name,
                false,
                new List<IAnnotation>(),
                attributes)
        { }

        public static IEnumerable<Declaration> GetSupertypes(Declaration type)
        {
            if (type.DeclarationType != DeclarationType.ClassModule)
            {
                return new List<Declaration>();
            }
            var classType = type as ClassModuleDeclaration;
            return classType != null ? classType.Supertypes : new List<Declaration>();
        }

        public static bool HasDefaultMember(Declaration type)
        {
            var classModule = type as ClassModuleDeclaration;
            return classModule?.DefaultMember != null;
        }

        public bool IsExtensible => _isExtensible.Value;

        private bool IsExtensibleToCache()
        {
            return HasAttribute("VB_Customizable");
        }

        /// <summary>
        /// Gets an attribute value indicating whether a class is exposed to other projects.
        /// If this value is false, any public types and members cannot be accessed from outside the project they're declared in.
        /// </summary>
        public bool IsExposed => _isExposed.Value;

        private bool IsExposedToCache()
        {
            if (!IsUserDefined)
            {
                return IsExposedForBuiltInModules;
            }
            return HasAttribute("VB_Exposed");
        }

        // TODO: This should only be a boolean in VBA ('Private' (false) and 'PublicNotCreatable' (true)) . For VB6 it will also need to support
        // 'SingleUse', 'GlobalSingleUse', 'MultiUse', and 'GlobalMultiUse'. See https://msdn.microsoft.com/en-us/library/aa234184%28v=vs.60%29.aspx
        // All built-ins are public (by definition).
        private static bool IsExposedForBuiltInModules { get; } = true;

        public bool IsControl { get; private set; }

        private bool HasAttribute(string attributeName)
        {
            var hasAttribute = false;
            IEnumerable<string> value;
            if (Attributes.TryGetValue(attributeName, out value))
            {
                hasAttribute = value.Single() == "True";
            }
            return hasAttribute;
        }             

        private bool? _isGlobal;
        private readonly object _isGlobalSyncObject = new object();
        public bool IsGlobalClassModule
        {
            get
            {
                lock (_isGlobalSyncObject)
                {
                    if (_isGlobal.HasValue)
                    {
                        return _isGlobal.Value;
                    }
                    _isGlobal = HasAttribute("VB_GlobalNamespace") || IsGlobalFromSubtypes();
                    return _isGlobal.Value;
                }
            }
        }

        private bool IsGlobalFromSubtypes()
        {
            return Subtypes.Any(subtype => subtype is ClassModuleDeclaration && ((ClassModuleDeclaration)subtype).IsGlobalClassModule);
        }

        /// <summary>
        /// Gets an attribute value indicating whether a class has a predeclared ID.
        /// Such classes can be treated as "static classes", or as far as resolving is concerned, as standard modules.
        /// </summary>
        public bool HasPredeclaredId => _hasPredeclaredId.Value;

        private bool HasPredeclaredIdToCache()
        {
            return HasAttribute("VB_PredeclaredId");
        }

        public bool HasDefaultInstanceVariable => HasPredeclaredId || IsGlobalClassModule;

        public Declaration DefaultMember { get; internal set; }

        //This is just convenience for the resolver to split gathering the names of the supertypes and resolving them.
        //todo: Find a cleaner solution for this.
        public IEnumerable<string> SupertypeNames => _supertypeNames;

        public IEnumerable<Declaration> Supertypes => _supertypes;

        public IEnumerable<Declaration> Subtypes => _subtypes.Keys;

        public void AddSupertypeName(string supertypeName)
        {
            _supertypeNames.Add(supertypeName);
        }

        public void AddSupertype(Declaration supertype)
        {
            (supertype as ClassModuleDeclaration)?.AddSubtype(this);
            _supertypes.Add(supertype);
        }

        public void ClearSupertypes()
        {
            foreach (var supertype in _supertypes)
            {
                (supertype as ClassModuleDeclaration)?.RemoveSubtype(this);
            }
            _supertypes.Clear();
        }

        private void AddSubtype(Declaration subtype)
        {
            InvalidateCachedIsGlobal();
            _subtypes.AddOrUpdate(subtype, 1, (key,value) => value);
        }

        private void RemoveSubtype(Declaration subtype)
        {
            InvalidateCachedIsGlobal();
            byte dummy;
            _subtypes.TryRemove(subtype, out dummy);
        }

        private void InvalidateCachedIsGlobal()
        {
            lock (_isGlobalSyncObject)
            {
                if (_isGlobal.HasValue)
                {
                    InvalidateCachedIsGlobalForSupertypes();    //If _isGlobal is not set, it has no influence on the state of the supertypes.
                    _isGlobal = null;
                }
            }
        }

        private void InvalidateCachedIsGlobalForSupertypes()
        {
            foreach(var supertype in Supertypes)
            {
                (supertype as ClassModuleDeclaration)?.InvalidateCachedIsGlobal();
            }
        }
    }
}
