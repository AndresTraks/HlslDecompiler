cs_5_0
dcl_globalFlags refactoringAllowed
dcl_uav_structured u0, 12
dcl_function_body fb0
dcl_function_body fb1
dcl_function_table ft0 = {fb0}
dcl_function_table ft1 = {fb1}
dcl_interface fp0[1][1] = {ft0, ft1}
dcl_input vThreadID.xyz
dcl_temps 2
dcl_thread_group 8, 1, 1
utof r0.xyz, vThreadID.xyz
fcall fp0[0][0]
store_structured u0.xyz, vThreadID.xxx, l(0), r1.xyz
ret
label fb0
mov r0.w, this[0].y
mov r1.w, this[0].x
add r1.xyz, r0.zyx, cb[r1.w][r0.w].xxx
ret
label fb1
mov r0.w, this[0].y
mov r1.w, this[0].x
mul r1.xyz, r0.xyz, cb[r1.w][r0.w].xyz
ret
