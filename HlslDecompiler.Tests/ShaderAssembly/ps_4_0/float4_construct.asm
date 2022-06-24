ps_4_0
dcl_input_ps linear v0
dcl_output o0
dcl_output o1
dcl_output o2
dcl_output o3
mov o0, v0
mov o1.xyz, v0.xyz
mov o1.w, l(0)
mov o2.xy, v0.xy
mov o2.zw, l(0, 0, 0, 1)
mov o3.x, v0.x
mov o3.yzw, l(0, 0, 1, 2)
ret
