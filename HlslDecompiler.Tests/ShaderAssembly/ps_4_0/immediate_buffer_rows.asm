ps_4_0
dcl_immediateConstantBuffer { { 1, 0, 0, 0 }, { 0, 1, 0, 0 }, { 0, 0, 1, 0 }, { 0.25, 0.5, 0.25, 0 }, { 0.5, 0.25, 0.25, 0 }, { 0.25, 0.25, 0.5, 0 } }
dcl_constantbuffer CB0[1], immediateIndexed
dcl_input_ps linear v0.x
dcl_output o0
dcl_temps 2
mov r0, l(0, 0, 0, 0)
loop
uge r1.x, r0.w, cb0[0].x
breakc_nz r1.x
mad r1.xyz, icb[r0.w].xyz, cb0[0].yyy, icb[r0.w + 3].xyz
add r0.xyz, r0.xyz, r1.xyz
iadd r0.w, r0.w, l(1)
endloop
mul o0.xyz, r0.xyz, v0.xxx
mov o0.w, l(0)
ret
