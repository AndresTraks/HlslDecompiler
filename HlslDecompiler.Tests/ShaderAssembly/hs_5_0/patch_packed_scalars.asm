hs_5_0
hs_decls
dcl_input_control_point_count 3
dcl_output_control_point_count 3
dcl_tessellator_domain domain_tri
dcl_tessellator_partitioning partitioning_integer
dcl_tessellator_output_primitive output_triangle_cw
dcl_globalFlags refactoringAllowed
dcl_immediateConstantBuffer { { 6, 5, 2, 0 }, { 0, 8, 3, 0 }, { 0, 0, 4, 0 }, { 0, 0, 7, 0 } }
hs_fork_phase
dcl_hs_fork_phase_instance_count 3
dcl_input vForkInstanceID
dcl_output_siv o0.x, finalTriUeq0EdgeTessFactor
dcl_output_siv o1.x, finalTriVeq0EdgeTessFactor
dcl_output_siv o2.x, finalTriWeq0EdgeTessFactor
dcl_temps 1
dcl_indexrange o0 3
mov r0.x, vForkInstanceID.x
mov o[r0.x].x, l(1)
ret
hs_fork_phase
dcl_output_siv o3.x, finalTriInsideTessFactor
mov o3.x, l(1)
ret
hs_fork_phase
dcl_hs_fork_phase_instance_count 4
dcl_input vForkInstanceID
dcl_output o0.y
dcl_output o1.y
dcl_output o2.y
dcl_output o4.y
dcl_temps 1
dcl_indexrange o0 5
ult r0.x, vForkInstanceID.x, l(3)
movc r0.x, r0.x, vForkInstanceID.x, l(4)
mov r0.y, vForkInstanceID.x
mov o[r0.x].y, icb[r0.y].z
ret
hs_fork_phase
dcl_hs_fork_phase_instance_count 2
dcl_input vForkInstanceID
dcl_output o2.z
dcl_output o4.z
dcl_temps 1
dcl_indexrange o2 3
ult r0.x, vForkInstanceID.x, l(1)
movc r0.x, r0.x, l(0), l(2)
mov r0.y, vForkInstanceID.x
mov o[r0.x + 2].z, icb[r0.y].y
ret
hs_fork_phase
dcl_output o4.x
mov o4.x, l(6)
ret
hs_fork_phase
dcl_output o4.w
mov o4.w, l(9)
ret
