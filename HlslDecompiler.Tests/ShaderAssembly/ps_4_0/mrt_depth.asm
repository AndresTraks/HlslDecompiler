ps_4_0
dcl_constantbuffer CB0[1], immediateIndexed
dcl_input_ps_siv linear noperspective v0.z, position
dcl_input_ps linear v1.xyz
dcl_input_ps linear v2.xy
dcl_output o0
dcl_output o1
dcl_output oDepth
dcl_temps 1
mov r0.xy, v2.xy
mov r0.zw, l(0, 0, 0, 1)
mul o0, r0, cb0[0]
dp3 r0.x, v1.xyz, v1.xyz
rsq r0.x, r0.x
mul r0.xyz, r0.xxx, v1.xyz
mad o1.xyz, r0.xyz, l(0.5, 0.5, 0.5, 0), l(0.5, 0.5, 0.5, 0)
mov o1.w, l(1)
mul oDepth, v0.z, l(0.5)
ret
