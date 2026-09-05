ps_4_0
dcl_constantbuffer cb0[5], immediateIndexed
dcl_input_ps linear v0.xyz
dcl_output o0
dcl_temps 1
lt r0.x, cb0[4].x, v0.x
if_nz r0.x
lt r0.x, cb0[4].x, v0.y
if_nz r0.x
lt r0.x, cb0[4].x, v0.z
if_nz r0.x
mov o0, cb0[0]
ret
else
mov o0, cb0[1]
ret
endif
endif
mov o0, cb0[2]
ret
endif
mov o0, cb0[3]
ret
