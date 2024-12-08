ps_4_0
dcl_input_ps linear v0.xy
dcl_input_ps linear v1.zw
dcl_output o0
dcl_output o1
dcl_output o2
dcl_output o3
dcl_temps 1
mov r0.zw, v0.xy
mov r0.xy, l(0, 1, 0, 0)
mov o0, r0.zxyw
mov o1, r0
mov o2.xyz, l(0, 1, 2, 0)
mov o2.w, v0.x
mov o3.xy, v0.xy
mov o3.zw, v1.zw
ret
