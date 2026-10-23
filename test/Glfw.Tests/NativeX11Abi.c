// Compile against the installed client headers, including on cross targets.
#include <X11/Xlib.h>
#include <X11/Xutil.h>
#include <X11/XKBlib.h>
#include <X11/extensions/XKBstr.h>

// LLVM constants make cross-target results readable without executing them.
#define METRIC(name, expression) const unsigned name = (expression)
METRIC(atom_bytes, sizeof(Atom));
METRIC(atom_alignment, _Alignof(Atom));
METRIC(keysym_bytes, sizeof(KeySym));
METRIC(time_bytes, sizeof(Time));
METRIC(xid_bytes, sizeof(XID));
METRIC(long_bytes, sizeof(long));
METRIC(ximstyle_bytes, sizeof(XIMStyle));
#define RECORD(T) METRIC(T##___sizeof, sizeof(T)); METRIC(T##___alignof, _Alignof(T))
#define FIELD(T, F) METRIC(T##_##F, __builtin_offsetof(T, F))
#define ARRAY(T, F) FIELD(T,F); METRIC(T##_##F##___sizeof, sizeof(((T*)0)->F)); METRIC(T##_##F##___element, sizeof(((T*)0)->F[0])); METRIC(T##_##F##___alignof, __alignof__(((T*)0)->F))
RECORD(XkbNamesRec);
FIELD(XkbNamesRec,keycodes); FIELD(XkbNamesRec,geometry);
FIELD(XkbNamesRec,symbols); FIELD(XkbNamesRec,types); FIELD(XkbNamesRec,compat);
ARRAY(XkbNamesRec,vmods); ARRAY(XkbNamesRec,indicators); ARRAY(XkbNamesRec,groups);
FIELD(XkbNamesRec,keys); FIELD(XkbNamesRec,key_aliases);
FIELD(XkbNamesRec,radio_groups); FIELD(XkbNamesRec,phys_symbols);
FIELD(XkbNamesRec,num_keys); FIELD(XkbNamesRec,num_key_aliases); FIELD(XkbNamesRec,num_rg);
RECORD(XkbDescRec);
FIELD(XkbDescRec,dpy); FIELD(XkbDescRec,flags); FIELD(XkbDescRec,device_spec);
FIELD(XkbDescRec,min_key_code); FIELD(XkbDescRec,max_key_code);
FIELD(XkbDescRec,ctrls); FIELD(XkbDescRec,server); FIELD(XkbDescRec,map);
FIELD(XkbDescRec,indicators); FIELD(XkbDescRec,names); FIELD(XkbDescRec,compat); FIELD(XkbDescRec,geom);
RECORD(XkbKeyNameRec); ARRAY(XkbKeyNameRec,name);
RECORD(XkbKeyAliasRec); ARRAY(XkbKeyAliasRec,real); ARRAY(XkbKeyAliasRec,alias);
RECORD(XkbStateRec);
FIELD(XkbStateRec,group); FIELD(XkbStateRec,locked_group);
FIELD(XkbStateRec,base_group); FIELD(XkbStateRec,latched_group);
FIELD(XkbStateRec,mods); FIELD(XkbStateRec,base_mods); FIELD(XkbStateRec,latched_mods);
FIELD(XkbStateRec,locked_mods); FIELD(XkbStateRec,compat_state);
FIELD(XkbStateRec,grab_mods); FIELD(XkbStateRec,compat_grab_mods);
FIELD(XkbStateRec,lookup_mods); FIELD(XkbStateRec,compat_lookup_mods); FIELD(XkbStateRec,ptr_buttons);
RECORD(XkbStateNotifyEvent);
FIELD(XkbStateNotifyEvent,type); FIELD(XkbStateNotifyEvent,serial); FIELD(XkbStateNotifyEvent,send_event);
FIELD(XkbStateNotifyEvent,display); FIELD(XkbStateNotifyEvent,time); FIELD(XkbStateNotifyEvent,xkb_type);
FIELD(XkbStateNotifyEvent,device); FIELD(XkbStateNotifyEvent,changed); FIELD(XkbStateNotifyEvent,group);
FIELD(XkbStateNotifyEvent,base_group); FIELD(XkbStateNotifyEvent,latched_group); FIELD(XkbStateNotifyEvent,locked_group);
FIELD(XkbStateNotifyEvent,mods); FIELD(XkbStateNotifyEvent,base_mods); FIELD(XkbStateNotifyEvent,latched_mods);
FIELD(XkbStateNotifyEvent,locked_mods); FIELD(XkbStateNotifyEvent,compat_state);
FIELD(XkbStateNotifyEvent,grab_mods); FIELD(XkbStateNotifyEvent,compat_grab_mods);
FIELD(XkbStateNotifyEvent,lookup_mods); FIELD(XkbStateNotifyEvent,compat_lookup_mods);
FIELD(XkbStateNotifyEvent,ptr_buttons); FIELD(XkbStateNotifyEvent,keycode);
FIELD(XkbStateNotifyEvent,event_type); FIELD(XkbStateNotifyEvent,req_major); FIELD(XkbStateNotifyEvent,req_minor);
RECORD(XEvent); METRIC(XEvent_padding___sizeof, sizeof(((XEvent*)0)->pad));
METRIC(XEvent_padding___alignof, __alignof__(((XEvent*)0)->pad));
RECORD(XClientMessageEvent);
FIELD(XClientMessageEvent,type); FIELD(XClientMessageEvent,serial); FIELD(XClientMessageEvent,send_event);
FIELD(XClientMessageEvent,display); FIELD(XClientMessageEvent,window);
FIELD(XClientMessageEvent,message_type); FIELD(XClientMessageEvent,format);
METRIC(XClientMessageEvent_l, __builtin_offsetof(XClientMessageEvent,data));
METRIC(XClientMessageEvent_l___sizeof, sizeof(((XClientMessageEvent*)0)->data.l));
METRIC(XClientMessageEvent_l___element, sizeof(((XClientMessageEvent*)0)->data.l[0]));
METRIC(XClientMessageEvent_l___alignof, __alignof__(((XClientMessageEvent*)0)->data.l));
RECORD(XIMStyles); FIELD(XIMStyles,count_styles); FIELD(XIMStyles,supported_styles);
// The production initializer uses a private record with managed field names.
typedef XIMStyles XIMStylesActive;
RECORD(XIMStylesActive);
METRIC(XIMStylesActive_Count, __builtin_offsetof(XIMStyles,count_styles));
METRIC(XIMStylesActive_Styles, __builtin_offsetof(XIMStyles,supported_styles));
RECORD(XIMCallback);
METRIC(XIMCallback_Data, __builtin_offsetof(XIMCallback,client_data));
METRIC(XIMCallback_Callback, __builtin_offsetof(XIMCallback,callback));
