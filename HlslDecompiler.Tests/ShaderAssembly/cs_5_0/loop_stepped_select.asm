cs_5_0
dcl_globalFlags refactoringAllowed
dcl_immediateConstantBuffer { { 1, 0, 0, 0 }, { 0, 1, 0, 0 }, { 0, 0, 1, 0 }, { 0, 0, 0, 1 } }
dcl_constantbuffer CB0[2], immediateIndexed
dcl_uav_structured u0, 4
dcl_input vThreadID.x
dcl_temps 1
dcl_thread_group 8, 1, 1
utof r0.x, vThreadID.x
add r0.x, r0.x, cb0[1].x
mov r0.yz, l(0, 0, 0, 0)
loop
ige r0.w, r0.z, l(4)
breakc_nz r0.w
dp4 r0.w, cb0[0], icb[r0.z]
lt r0.w, r0.w, r0.x
iadd r0.z, r0.z, l(1)
movc r0.y, r0.w, r0.z, r0.y
endloop
store_structured u0.x, vThreadID.x, l(0), r0.y
ret
