ps_4_0
dcl_input_ps linear v0.xy
dcl_input_ps linear v1.zw
dcl_output o0.xyzw
dcl_output o1.xyzw
dcl_output o2.xyzw
dcl_output o3.xyzw
dcl_temps 1
mov r0.zw, v0.xxxy
mov r0.xy, l(0,1.000000,0,0)
mov o0.xyzw, r0.zxyw
mov o1.xyzw, r0.xyzw
mov o2.xyz, l(0,1.000000,2.000000,0)
mov o2.w, v0.x
mov o3.xy, v0.xyxx
mov o3.zw, v1.zzzw
ret 
