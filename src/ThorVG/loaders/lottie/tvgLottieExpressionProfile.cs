using System;
using System.Collections.Generic;
using Jint;
using Jint.Native;
using Jint.Native.Symbol;
using Jint.Runtime;

namespace ThorVG;

// The pinned JerryScript profile omits these newer builtin entry points.
// This is configuration of the approved interpreter substitution, not a
// replacement/stub for any native supported algorithm.
internal static class LottieExpressionProfile
{
    private static readonly (string Path, string Members)[] Profile =
    [
        ("Object", "assign,create,defineProperties,defineProperty,entries,freeze,fromEntries,getOwnPropertyDescriptor,getOwnPropertyDescriptors,getOwnPropertyNames,getOwnPropertySymbols,getPrototypeOf,hasOwn,is,isExtensible,isFrozen,isSealed,keys,length,name,preventExtensions,prototype,seal,setPrototypeOf,values"),
        ("Object.prototype", "__proto__,constructor,hasOwnProperty,isPrototypeOf,propertyIsEnumerable,toLocaleString,toString,valueOf"),
        ("Function", "length,name,prototype"),
        ("Function.prototype", "apply,arguments,bind,call,caller,constructor,length,name,toString"),
        ("Array", "from,isArray,length,name,of,prototype"),
        ("Array.prototype", "at,concat,constructor,copyWithin,entries,every,fill,filter,find,findIndex,flat,flatMap,forEach,includes,indexOf,join,keys,lastIndexOf,length,map,pop,push,reduce,reduceRight,reverse,shift,slice,some,sort,splice,toLocaleString,toString,unshift,values"),
        ("Array.prototype[Symbol.unscopables]", "at,copyWithin,entries,fill,find,findIndex,flat,flatMap,includes,keys,values"),
        ("String", "fromCharCode,fromCodePoint,length,name,prototype,raw"),
        ("String.prototype", "at,charAt,charCodeAt,codePointAt,concat,constructor,endsWith,includes,indexOf,lastIndexOf,length,localeCompare,matchAll,padEnd,padStart,repeat,slice,split,startsWith,substring,toLocaleLowerCase,toLocaleUpperCase,toLowerCase,toString,toUpperCase,trim,trimEnd,trimLeft,trimRight,trimStart,valueOf"),
        ("Number", "EPSILON,MAX_SAFE_INTEGER,MAX_VALUE,MIN_SAFE_INTEGER,MIN_VALUE,NEGATIVE_INFINITY,NaN,POSITIVE_INFINITY,isFinite,isInteger,isNaN,isSafeInteger,length,name,parseFloat,parseInt,prototype"),
        ("Number.prototype", "constructor,toExponential,toFixed,toLocaleString,toPrecision,toString,valueOf"),
        ("Boolean.prototype", "constructor,toString,valueOf"),
        ("Math", "E,LN10,LN2,LOG10E,LOG2E,PI,SQRT1_2,SQRT2,abs,acos,acosh,asin,asinh,atan,atan2,atanh,cbrt,ceil,clz32,cos,cosh,exp,expm1,floor,fround,hypot,imul,log,log10,log1p,log2,max,min,pow,random,round,sign,sin,sinh,sqrt,tan,tanh,trunc"),
        ("Symbol", "asyncIterator,for,hasInstance,isConcatSpreadable,iterator,keyFor,length,match,matchAll,name,prototype,replace,search,species,split,toPrimitive,toStringTag,unscopables"),
        ("Symbol.prototype", "constructor,description,toString,valueOf"),
        ("Error", "length,name,prototype"),
        ("Error.prototype", "constructor,message,name,toString"),
        ("Map", "length,name,prototype"),
        ("Map.prototype", "clear,constructor,delete,entries,forEach,get,has,keys,set,size,values"),
        ("Set", "length,name,prototype"),
        ("Set.prototype", "add,clear,constructor,delete,entries,forEach,has,keys,size,values"),
        ("WeakMap", "length,name,prototype"),
        ("WeakSet", "length,name,prototype"),
        ("Promise", "all,allSettled,any,length,name,prototype,race,reject,resolve"),
        ("Promise.prototype", "catch,constructor,finally,then"),
        ("ArrayBuffer", "isView,length,name,prototype"),
        ("ArrayBuffer.prototype", "byteLength,constructor,slice"),
        ("Int8Array", "BYTES_PER_ELEMENT,length,name,prototype"),
        ("Float32Array", "BYTES_PER_ELEMENT,length,name,prototype"),
        ("Boolean", "length,name,prototype"),
        ("WeakMap.prototype", "constructor,delete,get,has,set"),
        ("WeakSet.prototype", "add,constructor,delete,has"),
        ("Int8Array.prototype", "BYTES_PER_ELEMENT,constructor"),
        ("Float32Array.prototype", "BYTES_PER_ELEMENT,constructor"),
        ("Object.getPrototypeOf(Int8Array)", "from,length,name,of,prototype"),
        ("Object.getPrototypeOf(Int8Array.prototype)", "at,buffer,byteLength,byteOffset,constructor,copyWithin,entries,every,fill,filter,find,findIndex,forEach,includes,indexOf,join,keys,lastIndexOf,length,map,reduce,reduceRight,reverse,set,slice,some,sort,subarray,toLocaleString,toString,values"),
        ("this", "$bm_add,$bm_div,$bm_mod,$bm_mul,$bm_rt,$bm_sub,$bm_sum,Array,ArrayBuffer,Boolean,Error,Float32Array,Function,Infinity,Int16Array,Int32Array,Int8Array,Map,Math,NaN,Number,Object,Promise,Set,String,Symbol,Uint16Array,Uint32Array,Uint8Array,Uint8ClampedArray,WeakMap,WeakSet,add,clamp,comp,content,cross,decodeURI,decodeURIComponent,degreesToRadians,div,dot,ease,easeIn,easeOut,effect,encodeURI,encodeURIComponent,eval,inPoint,index,isFinite,isNaN,key,length,linear,loopIn,loopInDuration,loopOut,loopOutDuration,mod,mul,nearestKey,normalize,numKeys,outPoint,parseFloat,parseInt,propertyGroup,propertyIndex,radiansToDegrees,random,speed,speedAtTime,sub,sum,temporalWiggle,thisComp,thisLayer,thisProperty,time,undefined,value,valueAtTime,velocity,velocityAtTime,wiggle"),
    ];

    internal static void Apply(Engine engine)
    {
        // Configure the realm before any user expression. This host API also
        // removes fixed well-known-symbol properties absent in the native
        // profile; ordinary JavaScript delete correctly cannot remove those.
        // It updates Jint's property shape/cache metadata as well.
        foreach (var item in Profile)
        {
            var target = engine.Evaluate(item.Path).AsObject();
            var allowed = new HashSet<string>(item.Members.Split(','), StringComparer.Ordinal);
            foreach (var key in target.GetOwnPropertyKeys(Types.String))
                if (!allowed.Contains(key.AsString())) target.RemoveOwnProperty(key);
        }

        // These prototypes are reachable through ordinary iterator instances,
        // even though their newer constructor globals were removed above.
        // Native exposes only the identity iterator symbol on each base;
        // concrete Array/String/Map/Set/Generator next/return/throw algorithms
        // remain the actual Jint implementations on their own prototypes.
        ConfigureIterator("Object.getPrototypeOf(Object.getPrototypeOf([][Symbol.iterator]()))", GlobalSymbolRegistry.Iterator);
        ConfigureIterator("Object.getPrototypeOf(Object.getPrototypeOf(Object.getPrototypeOf((async function*(){})())))", GlobalSymbolRegistry.AsyncIterator);

        void ConfigureIterator(string path, JsValue symbol)
        {
            var target = engine.Evaluate(path).AsObject();
            foreach (var key in target.GetOwnPropertyKeys(Types.String | Types.Symbol))
                if (!key.Equals(symbol)) target.RemoveOwnProperty(key);
        }
    }
}
