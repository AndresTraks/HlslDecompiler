ps_4_0
dcl_constantbuffer cb0[2], immediateIndexed
dcl_input_ps linear v0.x
dcl_output o0
dcl_temps 1
mov r0.xy, l(0, 0, 0, 0)
loop
ige r0.z, r0.y, cb0[1].x
breakc_nz r0.z
add r0.x, r0.x, v0.x
iadd r0.y, r0.y, l(1)
endloop
lt r0.x, r0.x, l(0)
discard_nz r0.x
mov o0, cb0[0]
ret
