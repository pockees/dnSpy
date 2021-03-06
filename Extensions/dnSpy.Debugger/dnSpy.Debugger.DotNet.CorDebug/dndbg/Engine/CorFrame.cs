﻿/*
    Copyright (C) 2014-2017 de4dot@gmail.com

    This file is part of dnSpy

    dnSpy is free software: you can redistribute it and/or modify
    it under the terms of the GNU General Public License as published by
    the Free Software Foundation, either version 3 of the License, or
    (at your option) any later version.

    dnSpy is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with dnSpy.  If not, see <http://www.gnu.org/licenses/>.
*/

using System;
using System.Collections.Generic;
using System.Diagnostics;
using dndbg.COM.CorDebug;
using dndbg.COM.MetaData;

namespace dndbg.Engine {
	sealed class CorFrame : COMObject<ICorDebugFrame>, IEquatable<CorFrame> {
		/// <summary>
		/// Gets the frame that this frame called or null
		/// </summary>
		public CorFrame Callee {
			get {
				int hr = obj.GetCallee(out var calleeFrame);
				return hr < 0 || calleeFrame == null ? null : new CorFrame(calleeFrame);
			}
		}

		/// <summary>
		/// Gets the frame that called this frame or null
		/// </summary>
		public CorFrame Caller {
			get {
				int hr = obj.GetCaller(out var callerFrame);
				return hr < 0 || callerFrame == null ? null : new CorFrame(callerFrame);
			}
		}

		/// <summary>
		/// Gets its chain
		/// </summary>
		public CorChain Chain {
			get {
				int hr = obj.GetChain(out var chain);
				return hr < 0 || chain == null ? null : new CorChain(chain);
			}
		}

		/// <summary>
		/// true if it has been neutered
		/// </summary>
		public bool IsNeutered {
			get {
				int hr = obj.GetChain(out var chain);
				return hr == CordbgErrors.CORDBG_E_OBJECT_NEUTERED;
			}
		}

		/// <summary>
		/// Gets the token of the method or 0
		/// </summary>
		public uint Token => token;
		readonly uint token;

		/// <summary>
		/// Start address of the stack segment
		/// </summary>
		public ulong StackStart => rangeStart;
		readonly ulong rangeStart;

		/// <summary>
		/// End address of the stack segment
		/// </summary>
		public ulong StackEnd => rangeEnd;
		readonly ulong rangeEnd;

		/// <summary>
		/// true if this is an IL frame (<see cref="ICorDebugILFrame"/>)
		/// </summary>
		public bool IsILFrame => obj is ICorDebugILFrame;

		/// <summary>
		/// true if this is a Native frame (<see cref="ICorDebugNativeFrame"/>). This can be true
		/// even if <see cref="IsILFrame"/> is true (it's a JIT-compiled frame).
		/// </summary>
		public bool IsNativeFrame => obj is ICorDebugNativeFrame;

		/// <summary>
		/// true if it's a JIT-compiled frame (<see cref="IsILFrame"/> and <see cref="IsNativeFrame"/>
		/// are both true).
		/// </summary>
		public bool IsJITCompiledFrame => IsILFrame && IsNativeFrame;

		/// <summary>
		/// true if this is an internal frame (<see cref="ICorDebugInternalFrame"/>)
		/// </summary>
		public bool IsInternalFrame => obj is ICorDebugInternalFrame;

		/// <summary>
		/// true if this is a runtime unwindable frame (<see cref="ICorDebugRuntimeUnwindableFrame"/>)
		/// </summary>
		public bool IsRuntimeUnwindableFrame => obj is ICorDebugRuntimeUnwindableFrame;

		/// <summary>
		/// Gets the IL frame IP. Only valid if <see cref="IsILFrame"/> is true
		/// </summary>
		public ILFrameIP ILFrameIP {
			get {
				var ilf = obj as ICorDebugILFrame;
				if (ilf == null)
					return new ILFrameIP();
				int hr = ilf.GetIP(out uint offset, out var mappingResult);
				return hr < 0 ? new ILFrameIP() : new ILFrameIP(offset, mappingResult);
			}
		}

		/// <summary>
		/// Gets the native frame IP. Only valid if <see cref="IsNativeFrame"/> is true
		/// </summary>
		public uint NativeFrameIP {
			get {
				var nf = obj as ICorDebugNativeFrame;
				if (nf == null)
					return 0;
				int hr = nf.GetIP(out uint offset);
				return hr < 0 ? 0 : offset;
			}
		}

		/// <summary>
		/// Gets the internal frame type or <see cref="CorDebugInternalFrameType.STUBFRAME_NONE"/>
		/// if it's not an internal frame (<see cref="ICorDebugInternalFrame"/>)
		/// </summary>
		public CorDebugInternalFrameType InternalFrameType {
			get {
				var @if = obj as ICorDebugInternalFrame;
				if (@if == null)
					return CorDebugInternalFrameType.STUBFRAME_NONE;
				int hr = @if.GetFrameType(out var type);
				return hr < 0 ? CorDebugInternalFrameType.STUBFRAME_NONE : type;
			}
		}

		/// <summary>
		/// Gets the function or null
		/// </summary>
		public CorFunction Function {
			get {
				int hr = obj.GetFunction(out var func);
				return hr < 0 || func == null ? null : new CorFunction(func);
			}
		}

		/// <summary>
		/// Gets the code or null
		/// </summary>
		public CorCode Code {
			get {
				int hr = obj.GetCode(out var code);
				return hr < 0 || code == null ? null : new CorCode(code);
			}
		}

		/// <summary>
		/// Gets all arguments. A returned argument could be null if there was an error
		/// </summary>
		public IEnumerable<CorValue> ILArguments {
			get {
				var ilf = obj as ICorDebugILFrame;
				if (ilf == null)
					yield break;
				int hr = ilf.EnumerateArguments(out var valueEnum);
				if (hr < 0)
					yield break;
				hr = valueEnum.GetCount(out uint totalCount);
				if (hr < 0)
					yield break;
				for (uint i = 0; i < totalCount; i++) {
					hr = valueEnum.Next(1, out var value, out uint count);
					if (hr != 0 || value == null)
						yield return null;
					else
						yield return new CorValue(value);
				}
			}
		}

		/// <summary>
		/// Gets all locals. A returned local could be null if there was an error
		/// </summary>
		public IEnumerable<CorValue> ILLocals {
			get {
				var ilf = obj as ICorDebugILFrame;
				if (ilf == null)
					yield break;
				int hr = ilf.EnumerateLocalVariables(out var valueEnum);
				if (hr < 0)
					yield break;
				hr = valueEnum.GetCount(out uint totalCount);
				if (hr < 0)
					yield break;
				for (uint i = 0; i < totalCount; i++) {
					hr = valueEnum.Next(1, out var value, out uint count);
					if (hr != 0 || value == null)
						yield return null;
					else
						yield return new CorValue(value);
				}
			}
		}

		/// <summary>
		/// Gets all type and/or method generic parameters. The first returned values are the generic
		/// type params, followed by the generic method params. See also <see cref="GetTypeAndMethodGenericParameters(out CorType[], out CorType[])"/>
		/// </summary>
		public IEnumerable<CorType> TypeParameters {
			get {
				var ilf2 = obj as ICorDebugILFrame2;
				if (ilf2 == null)
					yield break;
				int hr = ilf2.EnumerateTypeParameters(out var valueEnum);
				if (hr < 0)
					yield break;
				for (;;) {
					hr = valueEnum.Next(1, out var value, out uint count);
					if (hr != 0 || value == null)
						break;
					yield return new CorType(value);
				}
			}
		}

		public CorFrame(ICorDebugFrame frame)
			: base(frame) {
			int hr = frame.GetFunctionToken(out token);
			if (hr < 0)
				token = 0;

			hr = frame.GetStackRange(out rangeStart, out rangeEnd);
			if (hr < 0)
				rangeStart = rangeEnd = 0;
		}

		public CorStepper CreateStepper() {
			int hr = obj.CreateStepper(out var stepper);
			return hr < 0 || stepper == null ? null : new CorStepper(stepper);
		}

		/// <summary>
		/// Sets a new IL offset. All frames and chains for the current thread will be invalidated
		/// after this call. This method can only be called if <see cref="IsILFrame"/> is true.
		/// </summary>
		/// <param name="ilOffset">New IL offset</param>
		/// <returns></returns>
		public bool SetILFrameIP(uint ilOffset) {
			var ilf = obj as ICorDebugILFrame;
			if (ilf == null)
				return false;
			int hr = ilf.SetIP(ilOffset);
			return hr >= 0;
		}

		/// <summary>
		/// Returns true if it's safe to call <see cref="SetILFrameIP(uint)"/> but it can still be
		/// called if this method fails. This method can only be called if <see cref="IsILFrame"/>
		/// is true.
		/// </summary>
		/// <param name="ilOffset">IL offset</param>
		/// <returns></returns>
		public bool CanSetILFrameIP(uint ilOffset) {
			var ilf = obj as ICorDebugILFrame;
			if (ilf == null)
				return false;
			return ilf.CanSetIP(ilOffset) == 0;
		}

		/// <summary>
		/// Sets a new native offset. All frames and chains for the current thread will be invalidated
		/// after this call. This method can only be called if <see cref="IsNativeFrame"/> is true.
		/// </summary>
		/// <param name="offset">New offset</param>
		/// <returns></returns>
		public bool SetNativeFrameIP(uint offset) {
			var nf = obj as ICorDebugNativeFrame;
			if (nf == null)
				return false;
			int hr = nf.SetIP(offset);
			return hr >= 0;
		}

		/// <summary>
		/// Returns true if it's safe to call <see cref="SetNativeFrameIP(uint)"/> but it can still be
		/// called if this method fails. This method can only be called if <see cref="IsNativeFrame"/>
		/// is true.
		/// </summary>
		/// <param name="offset">Offset</param>
		/// <returns></returns>
		public bool CanSetNativeFrameIP(uint offset) {
			var nf = obj as ICorDebugNativeFrame;
			if (nf == null)
				return false;
			return nf.CanSetIP(offset) == 0;
		}

		/// <summary>
		/// Gets a local variable or null if it's not an <see cref="ICorDebugILFrame"/> or if there
		/// was an error
		/// </summary>
		/// <param name="index">Index of local</param>
		/// <param name="hr">Updated with error code</param>
		/// <returns></returns>
		public CorValue GetILLocal(uint index, out int hr) {
			var ilf = obj as ICorDebugILFrame;
			if (ilf == null) {
				hr = -1;
				return null;
			}
			hr = ilf.GetLocalVariable(index, out var value);
			return hr < 0 || value == null ? null : new CorValue(value);
		}

		/// <summary>
		/// Gets an argument or null if it's not an <see cref="ICorDebugILFrame"/> or if there
		/// was an error
		/// </summary>
		/// <param name="index">Index of argument</param>
		/// <param name="hr">Updated with error code</param>
		/// <returns></returns>
		public CorValue GetILArgument(uint index, out int hr) {
			var ilf = obj as ICorDebugILFrame;
			if (ilf == null) {
				hr = -1;
				return null;
			}
			hr = ilf.GetArgument(index, out var value);
			return hr < 0 || value == null ? null : new CorValue(value);
		}

		/// <summary>
		/// Gets all locals
		/// </summary>
		/// <param name="kind">Kind</param>
		public IEnumerable<CorValue> GetILLocals(ILCodeKind kind) {
			var ilf4 = obj as ICorDebugILFrame4;
			if (ilf4 == null)
				yield break;
			int hr = ilf4.EnumerateLocalVariablesEx(kind, out var valueEnum);
			if (hr < 0)
				yield break;
			for (;;) {
				hr = valueEnum.Next(1, out var value, out uint count);
				if (hr != 0 || value == null)
					break;
				yield return new CorValue(value);
			}
		}

		/// <summary>
		/// Gets a local variable or null if it's not an <see cref="ICorDebugILFrame4"/> or if there
		/// was an error
		/// </summary>
		/// <param name="kind">Kind</param>
		/// <param name="index">Index of local</param>
		/// <returns></returns>
		public CorValue GetILLocal(ILCodeKind kind, uint index) {
			var ilf4 = obj as ICorDebugILFrame4;
			if (ilf4 == null)
				return null;
			int hr = ilf4.GetLocalVariableEx(kind, index, out var value);
			return hr < 0 || value == null ? null : new CorValue(value);
		}

		/// <summary>
		/// Gets a local variable or null if it's not an <see cref="ICorDebugILFrame4"/> or if there
		/// was an error
		/// </summary>
		/// <param name="kind">Kind</param>
		/// <param name="index">Index of local</param>
		/// <returns></returns>
		public CorValue GetILLocal(ILCodeKind kind, int index) => GetILLocal(kind, (uint)index);

		/// <summary>
		/// Gets the code or null if it's not an <see cref="ICorDebugILFrame4"/> or if there was an
		/// error
		/// </summary>
		/// <param name="kind">Kind</param>
		/// <returns></returns>
		public CorCode GetCode(ILCodeKind kind) {
			var ilf4 = obj as ICorDebugILFrame4;
			if (ilf4 == null)
				return null;
			int hr = ilf4.GetCodeEx(kind, out var code);
			return hr < 0 || code == null ? null : new CorCode(code);
		}

		/// <summary>
		/// Splits up <see cref="TypeParameters"/> into type and method generic arguments
		/// </summary>
		/// <param name="typeGenArgs">Gets updated with a list containing all generic type arguments</param>
		/// <param name="methGenArgs">Gets updated with a list containing all generic method arguments</param>
		/// <returns></returns>
		public bool GetTypeAndMethodGenericParameters(out CorType[] typeGenArgs, out CorType[] methGenArgs) {
			var func = Function;
			var module = func?.Module;
			if (module == null) {
				typeGenArgs = Array.Empty<CorType>();
				methGenArgs = Array.Empty<CorType>();
				return false;
			}

			var mdi = module.GetMetaDataInterface<IMetaDataImport>();
			var gas = new List<CorType>(TypeParameters);
			var cls = func.Class;
			int typeGenArgsCount = cls == null ? 0 : MetaDataUtils.GetCountGenericParameters(mdi, cls.Token);
			int methGenArgsCount = MetaDataUtils.GetCountGenericParameters(mdi, func.Token);
			Debug.Assert(typeGenArgsCount + methGenArgsCount == gas.Count);
			typeGenArgs = new CorType[typeGenArgsCount];
			methGenArgs = new CorType[methGenArgsCount];
			int j = 0;
			for (int i = 0; j < gas.Count && i < typeGenArgs.Length; i++, j++)
				typeGenArgs[i] = gas[j];
			for (int i = 0; j < gas.Count && i < methGenArgs.Length; i++, j++)
				methGenArgs[i] = gas[j];

			return true;
		}

		public static bool operator ==(CorFrame a, CorFrame b) {
			if (ReferenceEquals(a, b))
				return true;
			if (ReferenceEquals(a, null) || ReferenceEquals(b, null))
				return false;
			return a.Equals(b);
		}

		public static bool operator !=(CorFrame a, CorFrame b) => !(a == b);
		public bool Equals(CorFrame other) => !ReferenceEquals(other, null) && RawObject == other.RawObject;
		public override bool Equals(object obj) => Equals(obj as CorFrame);
		public override int GetHashCode() => RawObject.GetHashCode();

		public T Write<T>(T output, TypeFormatterFlags flags, Func<DnEval> getEval = null) where T : ITypeOutput {
			new TypeFormatter(output, flags, getEval).Write(this);
			return output;
		}

		public string ToString(TypeFormatterFlags flags) => Write(new StringBuilderTypeOutput(), flags).ToString();
		public override string ToString() => ToString(TypeFormatterFlags.Default);
	}
}
