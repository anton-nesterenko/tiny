﻿using System;
using System.Collections;
using System.Collections.Generic;
using Tiny.Decompiler.Metadata.Layout;

namespace Tiny.Decompiler.Metadata
{
    //# Represents the collection of modules in an assembly.
    sealed class ModuleCollection : IReadOnlyList<Module>, IDisposable
    {
        //# Used to syncronize loading of the PE files for the non-manifest modules.
        object m_lockObject;

        //# The PE file containing the manifest module of the assembly. 
        PEFile m_mainFile;

        //# The list of other modules (besides the main module) in an asssembly. The array is loaded lazily. Each
        //# element will either be a PEFile (for meta-data modules) or a Module (for non meta-data modules).
        object[] m_otherModules;

        public ModuleCollection(PEFile mainFile)
        {
            try {
                m_mainFile = mainFile.CheckNotNull("mainFile");
                m_otherModules = new object[m_mainFile.GetRowCount(MetadataTable.File)];
                m_lockObject = new object();
            }
            catch {
                Dispose();
                throw;
            }
            
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public IEnumerator<Module> GetEnumerator()
        {
            CheckDisposed();
            for (int i = 0; i < Count; ++i) {
                yield return this[i];
            }
        }

        void CheckDisposed()
        {
            if (m_mainFile == null || m_mainFile.IsDisposed) {
                throw new ObjectDisposedException("ModuleCollection");
            }
        }

        public int Count
        {
            get { 
                CheckDisposed();
                return m_otherModules.Length + 1;
            }
        }

        public Module this[int index]
        {
            get
            {
                CheckDisposed();
                if (index < 0 || index > m_otherModules.Length) {
                    throw new IndexOutOfRangeException();
                }
                if (index == 0) {
                    return m_mainFile.Module;
                }
                LoadModule(index - 1);
                var ret = m_otherModules[index - 1];
                var peFile = ret as PEFile;
                if (peFile != null) {
                    return peFile.Module;
                }
                return ret.AssumeIs<Module>();
            }
        }

        private unsafe void LoadModule(int index)
        {
            if (m_otherModules[index] == null) {
                var f = m_mainFile.GetFileRow(index);
                if ((f->Flags & FileAttributes.ContainsNoMetadata) != 0) {
                    m_otherModules[index] = Module.CreateNonMetadataModule(m_mainFile.ReadSystemString(
                        f->GetNameOffset(m_mainFile)
                    ));
                }
                else {
                    m_otherModules[index] = new PEFile(m_mainFile.ReadSystemString(f->GetNameOffset(m_mainFile)));
                }
            }
        }

        //# Cleans up native resources used by the module collection.
        //# remarks: This will dispose resources for all contained modules, including the manifest module.
        //# It will invalidate the containing assembly and all objects it contains.
        public void Dispose()
        {
            if (m_mainFile != null) {
                m_mainFile.Dispose();
            }

            if (m_otherModules != null) {
                foreach (var obj in m_otherModules) {
                    var peFile = obj as PEFile;
                    if (peFile != null) {
                        peFile.Dispose();
                    }
                }
            }
            m_otherModules = null;
            m_mainFile = null;
        }
    }
}
