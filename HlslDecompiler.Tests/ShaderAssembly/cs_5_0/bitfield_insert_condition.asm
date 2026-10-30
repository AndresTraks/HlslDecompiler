cs_5_0
dcl_globalFlags refactoringAllowed
dcl_constantbuffer CB0[1], immediateIndexed
dcl_uav_structured u0, 4
dcl_input vThreadID.x
dcl_temps 2
dcl_thread_group 64, 1, 1
utof r0.x, vThreadID.x
mul r0.x, r0.x, cb0[0].x
and r0.y, cb0[0].y, l(15)
mov r0.z, r0.x
mov r0.w, l(0)
mov r1.x, l(0)
loop
uge r1.y, r1.x, r0.y
breakc_nz r1.y
add r1.y, r0.z, r0.z
frc r0.z, r1.y
ge r1.y, r0.z, l(0.5)
bfi r0.w, l(31), l(1), r0.w, r1.y
iadd r1.x, r1.x, l(1)
endloop
store_structured u0.x, vThreadID.x, l(0), r0.w
ret
