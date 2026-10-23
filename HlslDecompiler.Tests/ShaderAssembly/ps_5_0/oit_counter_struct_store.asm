ps_5_0
dcl_globalFlags refactoringAllowed | forceEarlyDepthStencil
dcl_constantbuffer CB0[1], immediateIndexed
dcl_uav_structured_opc u1, 12
dcl_uav_raw u2
dcl_input_ps_siv linear noperspective v0.xyz, position
dcl_input_ps linear v1
dcl_input vCoverage
dcl_output o0
dcl_temps 3
imm_atomic_alloc r0.x, u1
uge r0.y, r0.x, cb0[0].y
if_nz r0.y
mov o0, l(0, 0, 0, 0)
ret
endif
mov_sat r1, v1
mul r1, r1, l(255, 255, 255, 255)
ftou r1, r1
ftou r0.yz, v0.yx
imad r0.y, r0.y, cb0[0].x, r0.z
ishl r0.y, r0.y, l(2)
imm_atomic_exch r2.z, u2, r0.y, r0.x
imad r0.y, r1.y, l(256), r1.x
imad r0.y, r1.z, l(65536), r0.y
imad r2.x, r1.w, l(16777216), r0.y
countbits r0.y, vCoverage.x
utof r0.y, r0.y
mul r0.y, r0.y, v0.z
mul r2.y, r0.y, l(0.25)
store_structured u1.xyz, r0.xxx, l(0), r2.xyz
mov o0, l(0, 0, 0, 0)
ret
